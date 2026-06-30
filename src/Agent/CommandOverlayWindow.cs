// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MCEControl;

/// <summary>
/// The on-screen command overlay (#119): a borderless, top-most, <b>click-through</b>, alpha-blended
/// window that shows each MCEC command as it executes — the "MainWindow log view, tersified, larger
/// font." It subscribes to <see cref="CommandEventHub"/>, keeps a small <see cref="OverlayFeed"/>, and
/// paints it over the right ~30% of the primary screen with no border or scrollbars; old lines fade out.
///
/// <para>It registers its own handle with <see cref="WindowResolver"/> so an agent never sees or drives
/// its own overlay, and it never takes focus or input (WS_EX_NOACTIVATE + WS_EX_TRANSPARENT). Visual
/// transparency is the magenta <see cref="TransparencyKey"/> (fully click-through background) plus a
/// whole-window alpha; each line sits on a dark rounded scrim with a drop shadow so it stays readable
/// over light or dark content.</para>
/// </summary>
public sealed class CommandOverlayWindow : Form {
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;

    private static readonly Color KeyColor = Color.Magenta;

    private readonly OverlayFeed _feed = new(maxLines: 8, lifetime: TimeSpan.FromSeconds(8));
    private readonly Action<CommandEvent> _onEvent;
    private readonly System.Windows.Forms.Timer _ageTimer;

    public CommandOverlayWindow() {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = KeyColor;
        TransparencyKey = KeyColor;
        Opacity = 0.9;
        DoubleBuffered = true;
        Text = string.Empty;
        Bounds = OverlayLayout.RightFraction(Screen.PrimaryScreen!.WorkingArea, 0.30);

        _onEvent = OnCommandEvent;
        CommandEventHub.Subscribe(_onEvent);

        _ageTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _ageTimer.Tick += (_, _) => Invalidate();
        _ageTimer.Start();
    }

    /// <summary>Show without ever stealing focus from the app being driven.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams {
        get {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        WindowResolver.RegisterIgnoredWindow(Handle.ToInt64());
    }

    private void OnCommandEvent(CommandEvent ev) {
        if (IsDisposed || !IsHandleCreated) {
            return;
        }
        try {
            if (InvokeRequired) {
                BeginInvoke(_onEvent, ev);
                return;
            }
            _feed.Add(ev, DateTime.UtcNow);
            Invalidate();
        }
        catch (ObjectDisposedException) {
            // The window closed between the check and the marshal; nothing to draw.
        }
        catch (InvalidOperationException) {
            // Handle not ready; drop this line rather than risk throwing on the publisher's thread.
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.Clear(KeyColor); // keyed -> fully transparent / click-through
        System.Collections.Generic.IReadOnlyList<CommandEvent> lines = _feed.Visible(DateTime.UtcNow);
        if (lines.Count == 0) {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using Font font = new("Consolas", 14F, FontStyle.Bold, GraphicsUnit.Point);
        const int pad = 8;
        const int gap = 6;

        // Newest at the bottom; stack upward until we run out of room.
        float y = ClientSize.Height - pad;
        for (int i = lines.Count - 1; i >= 0; i--) {
            CommandEvent ev = lines[i];
            SizeF size = g.MeasureString(ev.TerseText, font, ClientSize.Width - pad * 2);
            float boxH = size.Height + pad;
            float boxW = Math.Min(size.Width + pad * 2, ClientSize.Width);
            float boxX = ClientSize.Width - boxW; // right-aligned
            float boxY = y - boxH;
            if (boxY < 0) {
                break;
            }

            using (SolidBrush scrim = new(Color.FromArgb(160, 18, 18, 20)))
            using (GraphicsPath path = RoundedRect(new RectangleF(boxX, boxY, boxW, boxH), 6f)) {
                g.FillPath(scrim, path);
            }

            Color fg = ev.Outcome switch {
                CommandOutcome.Failed => Color.FromArgb(255, 120, 120),
                CommandOutcome.Pending => Color.FromArgb(255, 214, 120),
                _ => Color.White,
            };
            float tx = boxX + pad;
            float ty = boxY + pad / 2f;
            using (SolidBrush shadow = new(Color.FromArgb(200, 0, 0, 0))) {
                g.DrawString(ev.TerseText, font, shadow, tx + 1.2f, ty + 1.2f);
            }
            using (SolidBrush brush = new(fg)) {
                g.DrawString(ev.TerseText, font, brush, tx, ty);
            }

            y = boxY - gap;
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius) {
        float d = radius * 2f;
        GraphicsPath p = new();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            CommandEventHub.Unsubscribe(_onEvent);
            _ageTimer?.Dispose();
            if (IsHandleCreated) {
                WindowResolver.UnregisterIgnoredWindow(Handle.ToInt64());
            }
        }
        base.Dispose(disposing);
    }
}
