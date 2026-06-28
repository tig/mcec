using System;
using System.Windows.Forms;

namespace MCEControl; 
/// <summary>
/// Enforce MaxLength property even when setting text programmatically
/// https://stackoverflow.com/questions/10011508/textbox-maximum-amount-of-characters-its-not-maxlength
/// </summary>
public class TextBoxExt : TextBox {
    new public void AppendText(string text) {
        if (this.Text.Length == this.MaxLength) {
            return;
        }
        else if (text != null && (this.Text.Length + text.Length > this.MaxLength)) {
            this.Clear();
            base.AppendText(text);
            //base.AppendText(text.Substring(0, (this.MaxLength - this.Text.Length)));
        }
        else {
            base.AppendText(text);
        }
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text {
        get {
            return base.Text;
        }
        set {
            if (!string.IsNullOrEmpty(value) && value.Length > this.MaxLength) {
                base.Text = value.Substring(0, this.MaxLength);
            }
            else {
                base.Text = value;
            }
        }
    }

    // Also: Clearing top X lines with high performance
    public void ClearTopLines(int count) {
        if (count <= 0) {
            return;
        }
        else if (!this.Multiline) {
            this.Clear();
            return;
        }

        string txt = this.Text;
        int cursor = 0, ixOf = 0, brkLength = 0, brkCount = 0;

        while (brkCount < count) {
            ixOf = txt.IndexOfBreak(cursor, out brkLength);
            if (ixOf < 0) {
                this.Clear();
                return;
            }
            cursor = ixOf + brkLength;
            brkCount++;
        }
        this.Text = txt.Substring(cursor);
    }
}
