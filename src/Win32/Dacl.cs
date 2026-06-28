using System;
using System.Collections;

namespace Microsoft.Win32.Security;
/// <summary>
/// Summary description for Dacl.
/// </summary>
#pragma warning disable CA1010, CA1710, CA1303
public class Dacl : Acl {
    internal Dacl(IntPtr pacl) : base(pacl) {
    }
    public Dacl() : base() {
    }
    /// <summary>
    ///  This algorithm was copied from ATL source code: CAdcl::PrepareAcesForACL.
    /// 
    ///  We can't use QuickSort (or any other n log (n)) generic sort algorithm
    ///  because we want partial ordering to be preserved. All we want to do is sort 
    ///  the elements according to their "Order" (see OrderAceAccess.Compare method),
    ///  but we want the elements which compare to "Equal" to remain in their
    ///  original order in the array.
    /// </summary>
    protected override void PrepareAcesForACL() {
        IComparer comparer = new OrderAceAccess();

        int nCount = this.AceCount;

        // Find first "h" such that 
        // 1. h * 3 + 1 < nCount
        // 2. (h - 1) is exactly divisible by 3
        int h = 1;
        while (h * 3 + 1 < nCount) {
            h = 3 * h + 1;
        }

        while (h > 0) {
            for (int i = h - 1; i < nCount; i++) {
                Ace pivot = this.GetAce(i);

                int j;
                for (j = i;
                    (j >= h) && (comparer.Compare(this.GetAce(j - h), pivot) > 0);
                    j -= h) {
                    this.SetAce(j, this.GetAce(j - h));
                }

                this.SetAce(j, pivot);
            }

            h /= 3;
        }
    }
    public void AddAce(AceAccess ace) {
        base.AddAce(ace);
    }
}
