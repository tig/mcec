using System;

namespace Microsoft.Win32.Security {
    using Win32Structs;

    /// <summary>
    /// Summary description for Luid.
    /// </summary>
    public class Luid {
        private readonly LUID _luid;
        public Luid(LUID luid) {
            _luid = luid;
        }
#pragma warning disable CS3003 // Identifier is not CLS-compliant
        public UInt64 Value {
            get {
                return (((UInt64)(_luid.HighPart)) << 32) | ((UInt64)_luid.LowPart);
            }
        }
        internal LUID GetNativeLUID() {
            return _luid;
        }
    }
}
