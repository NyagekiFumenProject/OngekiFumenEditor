using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Gekimini.Avalonia.Framework.Commands;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

/// <summary>
/// Pins the broadcast contract of <see cref="CommandManager"/>: one failing subscriber must not
/// cut the pass short, must not be able to leave requery switched off, and subscriptions that
/// change while a pass is notifying must not abort it.
/// </summary>
public sealed class CommandManagerRequeryTests
{
    [AvaloniaFact]
    public async Task Requery_KeepsNotifyingAfterAFailingSubscriber()
    {
        var reached = 0;
        EventHandler failing = (_, _) => throw new InvalidOperationException("probe subscriber failure");
        EventHandler counter = (_, _) => Interlocked.Increment(ref reached);

        CommandManager.RequerySuggested += failing;
        CommandManager.RequerySuggested += counter;
        try
        {
            var firstPass = await WaitForNotificationAsync(() => Volatile.Read(ref reached));
            Assert.True(firstPass.Delta > 0,
                $"a failing subscriber aborted the pass before the next subscriber ran; {firstPass.Describe()}");

            // Requery must stay usable: the failing subscriber must not disable every later pass.
            var secondPass = await WaitForNotificationAsync(() => Volatile.Read(ref reached));
            Assert.True(secondPass.Delta > 0,
                $"requery stopped notifying after a subscriber failed; {secondPass.Describe()}");
        }
        finally
        {
            CommandManager.RequerySuggested -= failing;
            CommandManager.RequerySuggested -= counter;
        }
    }

    [AvaloniaFact]
    public async Task Requery_ToleratesSubscriptionChangesDuringPass()
    {
        var reached = 0;
        EventHandler late = (_, _) => Interlocked.Add(ref reached, 100);
        EventHandler mutator = (_, _) =>
        {
            CommandManager.RequerySuggested -= late;
            CommandManager.RequerySuggested += late;
        };
        EventHandler counter = (_, _) => Interlocked.Increment(ref reached);

        CommandManager.RequerySuggested += mutator;
        CommandManager.RequerySuggested += counter;
        try
        {
            var notifications = await WaitForNotificationAsync(() => Volatile.Read(ref reached));
            Assert.True(notifications.Delta > 0,
                $"subscribing during a pass aborted the pass; {notifications.Describe()}");
        }
        finally
        {
            CommandManager.RequerySuggested -= mutator;
            CommandManager.RequerySuggested -= counter;
            CommandManager.RequerySuggested -= late;
        }
    }

    /// <summary>
    /// Requests requery passes until the observed counter advances. Requests raised while a pass is
    /// already in flight are collapsed into that pass by design, so a single request cannot prove
    /// notification happened. Failures raised by the dispatcher pump are swallowed and reported
    /// through <see cref="Outcome.Describe"/> instead, so a broken pass stays visible as a failure.
    /// </summary>
    private static async Task<Outcome> WaitForNotificationAsync(Func<int> observed)
    {
        var deadline = Stopwatch.StartNew();
        var baseline = observed();
        var pumpFailures = new List<string>();
        var iterations = 0;
        var onUiThread = Dispatcher.UIThread.CheckAccess();

        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            iterations++;
            CommandManager.InvalidateRequerySuggested("test");
            try
            {
                Dispatcher.UIThread.RunJobs();
            }
            catch (Exception exception)
            {
                pumpFailures.Add($"{exception.GetType().Name}: {exception.Message}");
            }

            if (observed() > baseline)
                return new Outcome(observed() - baseline, iterations, onUiThread, pumpFailures);

            await Task.Delay(20);
        }

        return new Outcome(0, iterations, onUiThread, pumpFailures);
    }

    private readonly record struct Outcome(
        int Delta,
        int Iterations,
        bool StartedOnUiThread,
        List<string> PumpFailures)
    {
        public string Describe()
        {
            var builder = new StringBuilder();
            builder.Append($"iterations={Iterations}, delta={Delta}, checkAccess={StartedOnUiThread}");
            builder.Append($", pumpFailures={PumpFailures.Count}");
            if (PumpFailures.Count > 0)
                builder.Append($" ({string.Join(" | ", PumpFailures)})");

            return builder.ToString();
        }
    }
}
