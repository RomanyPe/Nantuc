using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nantuc.Benchmarks;

public static partial class BenchmarkOptimizer
{
    public static void OptimizeCurrentProcess()
    {
        Console.WriteLine("--- Оптимизация процесса для бенчмарка ---");

        // 1. Повышаем приоритет процесса
        TrySetHighPriority();

        // 2. Привязываем к конкретному ядру CPU (например, к последнему)
        TryBindToLastCpuCore();
    }

    private static void TrySetHighPriority()
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();

            // В Windows это работает из коробки. В Linux/macOS требует прав root/sudo, 
            // иначе выбросит исключение, которое мы отловим.
            currentProcess.PriorityClass = ProcessPriorityClass.High;
            Console.WriteLine($"[Успех] Приоритет процесса изменен на: {currentProcess.PriorityClass}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Предупреждение] Не удалось установить приоритет High: {ex.Message}");
            Console.WriteLine("На Linux/macOS для повышения приоритета запустите программу с 'sudo'.");
        }
    }

    private static void TryBindToLastCpuCore()
    {
        try
        {
            int coreCount = Environment.ProcessorCount;
            if (coreCount <= 1)
            {
                Console.WriteLine("[Инфо] Доступно только 1 ядро. Привязка не требуется.");
                return;
            }

            // Выбираем последнее ядро (индекс coreCount - 1), чтобы уйти от системного шума на ядрах 0, 1
            int targetCore = coreCount - 1;
            long affinityMask = 1L << targetCore;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var currentProcess = Process.GetCurrentProcess();
                currentProcess.ProcessorAffinity = checked((IntPtr)affinityMask);
                Console.WriteLine($"[Успех] [Windows] Процесс привязан к ядру CPU #{targetCore}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // В .NET на Linux ProcessorAffinity напрямую не реализован для записи, 
                // поэтому вызываем системную функцию sched_setaffinity через P/Invoke
                int pid = Environment.ProcessId;
                if (LinuxNative.sched_setaffinity(pid, (IntPtr)Marshal.SizeOf<IntPtr>(), ref affinityMask) == 0)
                {
                    Console.WriteLine($"[Успех] [Linux] Процесс привязан к ядру CPU #{targetCore}");
                }
                else
                {
                    Console.WriteLine("[Ошибка] [Linux] sched_setaffinity вернул ошибку.");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // В macOS (Darwin) классического Affinity Mask нет из-за архитектуры планировщика.
                // Вместо этого используются Affinity Tags. Потоки с одинаковым тегом планируются на одном L2/L3 кэше.
                int threadId = LinuxNative.pthread_self();
                IntPtr policyInfo = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(policyInfo, targetCore); // Используем номер ядра как тег сходства

                if (LinuxNative.thread_policy_set(threadId, THREAD_AFFINITY_POLICY, policyInfo, 1) == 0)
                {
                    Console.WriteLine($"[Успех] [macOS] Задан Affinity Tag #{targetCore} для текущего потока");
                }
                else
                {
                    Console.WriteLine("[Ошибка] [macOS] Не удалось задать thread_policy_set.");
                }
                Marshal.FreeHGlobal(policyInfo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка] Не удалось привязать к ядру: {ex.Message}");
        }
    }

    // Константа для macOS
    private const int THREAD_AFFINITY_POLICY = 4;

    // Импорт нативных функций для Unix-систем
    private static partial class LinuxNative
    {
        // Для Linux
        [LibraryImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
        public static partial int sched_setaffinity(int pid, IntPtr cpusetsize, ref long mask);

        // Для macOS
        [LibraryImport("libc", EntryPoint = "pthread_self")]
        public static partial int pthread_self();

        [LibraryImport("libc", EntryPoint = "thread_policy_set")]
        public static partial int thread_policy_set(int thread, int policy, IntPtr flavor, int count);
    }
}