using System;
using System.Collections;

namespace Microsoft.Win32.Security;
class OrderAceAccess : IComparer {
    public int Compare(object? o1, object? o2) {
        AceAccess lhs = (AceAccess)o1;
        AceAccess rhs = (AceAccess)o2;

        // The order is:
        // denied direct aces
        // denied direct object aces
        // allowed direct aces
        // allowed direct object aces
        // denied inherit aces
        // denied inherit object aces
        // allowed inherit aces
        // allowed inherit object aces

        // inherited aces are always "greater" than non-inherited aces
        if (lhs.IsInherited && !rhs.IsInherited) {
            return 1;
        }

        if (!lhs.IsInherited && rhs.IsInherited) {
            return -1;
        }

        // if the aces are *both* either inherited or non-inherited, continue...

        // allowed aces are always "greater" than denied aces (subject to above)
        if (lhs.IsAllowed && !rhs.IsAllowed) {
            return 1;
        }

        if (!lhs.IsAllowed && rhs.IsAllowed) {
            return -1;
        }

        // if the aces are *both* either allowed or denied, continue...

        // object aces are always "greater" than non-object aces (subject to above)
        if (lhs.IsObjectAce && !rhs.IsObjectAce) {
            return 1;
        }

        if (!lhs.IsObjectAce && rhs.IsObjectAce) {
            return -1;
        }

        // aces are "equal" (e.g., both are access denied inherited object aces)
        return 0;
    }
}
