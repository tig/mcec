using System;
using System.Windows.Forms;

namespace MCEControl;
public static class StringExt {
    public static int IndexOfBreak(this string str, out int length) {
        return IndexOfBreak(str, 0, out length);
    }

    public static int IndexOfBreak(this string str, int startIndex, out int length) {
        if (string.IsNullOrEmpty(str)) {
            length = 0;
            return -1;
        }
        int ub = str.Length - 1;
        int intchr;
        if (startIndex > ub) {
            throw new ArgumentOutOfRangeException();
        }
        for (int i = startIndex; i <= ub; i++) {
            intchr = str[i];
            if (intchr == 0x0D) {
                if (i < ub && str[i + 1] == 0x0A) {
                    length = 2;
                }
                else {
                    length = 1;
                }
                return i;
            }
            else if (intchr == 0x0A) {
                length = 1;
                return i;
            }
        }
        length = 0;
        return -1;
    }
}
