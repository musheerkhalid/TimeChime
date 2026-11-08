using System.Reflection;
using Xunit;

namespace TimeChime.Core.Tests;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("=== Running TimeChime Phase 1 Tests ===");

        int passed = 0;
        int failed = 0;

        var testTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Name.EndsWith("Tests"));

        foreach (var type in testTypes)
        {
            var methods = type.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(FactAttribute), false).Any());

            foreach (var method in methods)
            {
                object? instance = null;
                try
                {
                    instance = Activator.CreateInstance(type);
                    var result = method.Invoke(instance, null);
                    if (result is Task task)
                    {
                        task.GetAwaiter().GetResult();
                    }
                    Console.WriteLine($"[PASS] {type.Name}.{method.Name}");
                    passed++;
                }
                catch (TargetInvocationException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {type.Name}.{method.Name}: {ex.InnerException?.Message ?? ex.Message}");
                    Console.ResetColor();
                    failed++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {type.Name}.{method.Name}: {ex.Message}");
                    Console.ResetColor();
                    failed++;
                }
                finally
                {
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        Console.WriteLine("=======================================");
        Console.WriteLine($"Summary: {passed} passed, {failed} failed.");

        return failed == 0 ? 0 : 1;
    }
}

