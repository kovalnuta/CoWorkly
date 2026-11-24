using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Npgsql;

namespace CoWorkly.Data
{
    /// <summary>Результат проверки подключения к базе данных.</summary>
    public enum DbStatus
    {
        /// <summary>Подключение установлено.</summary>
        Ok,
        /// <summary>На адресе и порту никто не отвечает (контейнер не запущен, Docker выключен).</summary>
        Unreachable,
        /// <summary>Сервер отвечает, но отклоняет пользователя или пароль (28P01) либо нет базы (3D000).</summary>
        Rejected,
        /// <summary>Другая ошибка.</summary>
        Other
    }

    /// <summary>
    /// Подготовка локальной базы PostgreSQL в Docker перед запуском приложения.
    /// </summary>
    /// <remarks>
    /// Проверяет подключение; если база не запущена — сам запускает Docker Desktop
    /// и <c>docker compose up -d --wait</c>; если пароль не подходит (том базы создан
    /// со старыми настройками) — может пересоздать локальную базу.
    /// Файл docker-compose.yml ищется вверх от каталога программы, поэтому всё работает
    /// из любой папки, куда скопирован репозиторий.
    /// </remarks>
    public static class DatabaseBootstrap
    {
        /// <summary>Имя контейнера из docker-compose.yml (в том числе в старых копиях проекта).</summary>
        public const string ContainerName = "coworkly_db";

        private const string ComposeFile = "docker-compose.yml";

        /// <summary>Пробует открыть подключение (таймаут 3 с).</summary>
        public static DbStatus Check(string connectionString, out string details)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = 3,
                    Pooling = false
                };
                using var connection = new NpgsqlConnection(builder.ConnectionString);
                connection.Open();
                details = string.Empty;
                return DbStatus.Ok;
            }
            catch (PostgresException ex) when (ex.SqlState is "28P01" or "28000" or "3D000")
            {
                details = $"{ex.SqlState}: {ex.MessageText}";
                return DbStatus.Rejected;
            }
            catch (PostgresException ex) when (ex.SqlState is "57P03")
            {
                // «the database system is starting up» — контейнер ещё запускается
                details = ex.MessageText;
                return DbStatus.Unreachable;
            }
            catch (Exception ex) when (IsUnreachable(ex))
            {
                details = ex.Message;
                return DbStatus.Unreachable;
            }
            catch (Exception ex)
            {
                details = ex.Message;
                return DbStatus.Other;
            }
        }

        /// <summary>Ждёт, пока база станет доступной или явно отклонит подключение.</summary>
        public static DbStatus WaitUntilReady(string connectionString, TimeSpan timeout, out string details)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var status = Check(connectionString, out details);
                if (status != DbStatus.Unreachable || stopwatch.Elapsed > timeout)
                    return status;
                Thread.Sleep(1000);
            }
        }

        /// <summary>Каталог с docker-compose.yml: ищется от каталога программы вверх.</summary>
        public static string? FindComposeDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, ComposeFile)))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// Запускает контейнер базы. При необходимости запускает Docker Desktop и убирает
        /// контейнер с тем же именем, оставшийся от другой копии проекта.
        /// </summary>
        /// <returns><c>null</c> при успехе, иначе понятное описание проблемы.</returns>
        public static string? StartContainer(string composeDirectory)
        {
            var dockerProblem = EnsureDockerRunning();
            if (dockerProblem != null)
                return dockerProblem;

            var (code, output) = Docker("compose up -d --wait", composeDirectory, 120);
            if (code == 0)
                return null;

            // Контейнер coworkly_db или порт 5433 заняты контейнером из другой папки проекта
            if (output.Contains("already in use", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase))
            {
                Docker($"rm -f {ContainerName}", composeDirectory, 30);
                (code, output) = Docker("compose up -d --wait", composeDirectory, 120);
                if (code == 0)
                    return null;
            }

            return "Не удалось запустить контейнер базы данных:\n" + LastLines(output);
        }

        /// <summary>
        /// Пересоздаёт локальную базу: удаляет контейнер и его том, создаёт заново
        /// с настройками из docker-compose.yml. Все данные локальной базы удаляются.
        /// </summary>
        /// <returns><c>null</c> при успехе, иначе описание проблемы.</returns>
        public static string? ResetDatabase(string composeDirectory)
        {
            var dockerProblem = EnsureDockerRunning();
            if (dockerProblem != null)
                return dockerProblem;

            Docker($"rm -f {ContainerName}", composeDirectory, 30);
            Docker("compose down -v", composeDirectory, 60);

            var (code, output) = Docker("compose up -d --wait", composeDirectory, 180);
            return code == 0 ? null : "Не удалось пересоздать базу данных:\n" + LastLines(output);
        }

        // ---------------------------------------------------------------- Docker

        private static string? EnsureDockerRunning()
        {
            var (code, output) = Docker("info --format \"{{.ServerVersion}}\"", null, 15);
            if (code == 0)
                return null;
            if (code == -1)
                return "Docker не найден. Установите Docker Desktop: https://www.docker.com/products/docker-desktop/";

            // Docker установлен, но не запущен — пробуем запустить Docker Desktop сами
            var desktop = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Docker", "Docker", "Docker Desktop.exe");
            if (!File.Exists(desktop))
                return "Docker не запущен. Запустите Docker Desktop и откройте программу снова.\n" + LastLines(output);

            try
            {
                Process.Start(new ProcessStartInfo(desktop) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                return "Не удалось запустить Docker Desktop: " + ex.Message;
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(120))
            {
                Thread.Sleep(3000);
                if (Docker("info --format \"{{.ServerVersion}}\"", null, 15).Code == 0)
                    return null;
            }
            return "Docker Desktop не запустился за 2 минуты. Дождитесь «Engine running» и откройте программу снова.";
        }

        private static (int Code, string Output) Docker(string arguments, string? workingDirectory, int timeoutSeconds)
        {
            var info = new ProcessStartInfo("docker", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (workingDirectory != null)
                info.WorkingDirectory = workingDirectory;

            try
            {
                using var process = Process.Start(info)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    try { process.Kill(true); } catch { /* уже завершился */ }
                    return (-2, $"docker {arguments}: превышено время ожидания {timeoutSeconds} с");
                }
                return (process.ExitCode, stdout.Result + stderr.Result);
            }
            catch (Win32Exception)
            {
                return (-1, "docker не найден");
            }
        }

        private static bool IsUnreachable(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is SocketException or TimeoutException)
                    return true;
                if (e is NpgsqlException { IsTransient: true })
                    return true;
            }
            return false;
        }

        private static string LastLines(string text, int count = 6)
        {
            var lines = text.Trim().Split('\n');
            return string.Join("\n", lines[Math.Max(0, lines.Length - count)..]).Trim();
        }
    }
}
