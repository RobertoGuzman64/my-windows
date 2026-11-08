using System.Diagnostics;
using System.Text;
using MyWindows.Application.Repositories;

namespace MyWindows.Infrastructure;

public sealed class GitCommandRunner : IGitCommandRunner
{
    private const int MaximumOutputCharacters = 1024 * 1024;
    private static readonly TimeSpan MaximumExecutionTime = TimeSpan.FromSeconds(10);

    public GitCommandResult Run(string directory, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(arguments);

        string fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("El directorio solicitado no existe.");
        }

        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = fullPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string argument in arguments)
        {
            if (argument is null)
            {
                throw new ArgumentException("Los argumentos de Git no pueden ser nulos.", nameof(arguments));
            }

            start.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                throw new GitInspectionException("No se ha podido iniciar Git.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new GitInspectionException("Git no está disponible en este equipo.", exception);
        }

        using CancellationTokenSource cancellation = new(MaximumExecutionTime);
        try
        {
            Task<string> stdout = ReadLimitedAsync(process.StandardOutput, cancellation.Token);
            Task<string> stderr = ReadLimitedAsync(process.StandardError, cancellation.Token);
            Task.WhenAll(process.WaitForExitAsync(cancellation.Token), stdout, stderr)
                .GetAwaiter().GetResult();
            return new GitCommandResult(process.ExitCode, stdout.Result, stderr.Result);
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            throw new GitInspectionException("Git ha superado el tiempo máximo permitido.", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new GitInspectionException("La salida de Git excede el límite permitido.", exception);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        StringBuilder content = new();
        char[] buffer = new char[4096];
        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return content.ToString();
            }

            if (content.Length + count > MaximumOutputCharacters)
            {
                throw new InvalidDataException("Salida demasiado extensa.");
            }

            content.Append(buffer, 0, count);
        }
    }
}
