using System;

namespace ScriptStack.Runtime
{
    public enum ClrInteropMode
    {
        Disabled = 0,
        Safe = 1,
        Full = 2
    }

    /// <summary>
    /// Options for the ScriptStack interpreter.
    /// 
    /// Default: CLR interop is disabled (untrusted-by-default).
    /// </summary>
    public sealed class InterpreterOptions
    {
        public ClrInteropMode Clr { get; set; } = ClrInteropMode.Disabled;

        /// <summary>
        /// Optional custom CLR policy. If set, it overrides the default policy of the selected <see cref="ClrInteropMode"/>.
        /// </summary>
        public IClrPolicy? ClrPolicy { get; set; }

        internal IClrPolicy ResolveClrPolicy()
        {
            if (ClrPolicy != null)
                return ClrPolicy;

            return Clr switch
            {
                ClrInteropMode.Full => new AllowAllClrPolicy(),
                ClrInteropMode.Safe => new SafeClrPolicy(),
                _ => new DenyAllClrPolicy(),
            };
        }
    }
}
