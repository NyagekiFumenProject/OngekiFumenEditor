using Gekimini.Avalonia.Framework.Commands;

namespace OngekiFumenEditor.Avalonia.Utils;
/*
internal static class ActionExecutionContextExtensionMethod
{
    private sealed class DisableHandle : IDisposable
    {
        private object source;

        public DisableHandle(ActionExecutionContext ctx)
        {
            if (ctx.Source is null)
                return;

            source = ctx.Source;
            var prop = source.GetType().GetProperty("IsEnabled");
            if (prop?.CanWrite == true)
                prop.SetValue(source, false);
        }

        public void Dispose()
        {
            if (source is null)
                return;

            var prop = source.GetType().GetProperty("IsEnabled");
            if (prop?.CanWrite == true)
                prop.SetValue(source, true);
        }
    }

    public static IDisposable DisableSourceByDisposable(this ActionExecutionContext ctx)
    {
        return new DisableHandle(ctx);
    }
}
*/
