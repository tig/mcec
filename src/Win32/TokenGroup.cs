namespace Microsoft.Win32.Security {
    using Win32Structs;

    /// <summary>
    /// Summary description for TokenGroup.
    /// </summary>
    public class TokenGroup {
        private Sid _sid;
        private GroupAttributes _attributes;

        internal TokenGroup(MemoryMarshaler m) {
            var sa = (SID_AND_ATTRIBUTES)m.ParseStruct(typeof(SID_AND_ATTRIBUTES));
            _sid = new Sid(sa.Sid);
            _attributes = (GroupAttributes)sa.Attributes;
        }

        public Sid Sid {
            get {
                return _sid;
            }
        }
        public GroupAttributes Attributes {
            get {
                return _attributes;
            }
        }
    }
}
