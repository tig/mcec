// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

// The WS_EX_* constants below mirror the Win32 extended-window-style names; renaming them would break
// the 1:1 mapping to the Windows SDK headers.
// ReSharper disable InconsistentNaming

namespace MCEControl;

/// <summary>
/// The on-screen command overlay (#119): a borderless, top-most, <b>click-through</b>, alpha-blended
/// window that shows each MCEC command as it executes; the "MainWindow log view, tersified, larger
/// font." It subscribes to <see cref="CommandEventHub"/>, keeps a bounded <see cref="OverlayFeed"/>, and
/// paints the feed in a ~30% column hugging the docked (left or right) screen edge, with no border or
/// scrollbars; the newest command sits at the top and older ones scroll down and off the bottom.
///
/// <para>It is a true per-pixel-alpha layered window (<c>UpdateLayeredWindow</c>): each line sits on the
/// About box's burnt-orange brand colour at 30% alpha so the desktop shows through, while the text stays
/// fully opaque and readable. It registers its own handle with <see cref="WindowResolver"/> so an agent
/// never sees or drives its own overlay, and it never takes focus or input (WS_EX_NOACTIVATE +
/// WS_EX_TRANSPARENT).</para>
///
/// <para>The window spans the full screen <see cref="Screen.Bounds"/> (not the working area, so it sits
/// over the taskbar too; full width so the banner can center across the whole screen), inset by the
/// layout margin, and re-asserts top-most Z-order on every paint (<see cref="PushLayered"/>), so a window
/// created top-most AFTER the overlay cannot occlude it; a one-time WS_EX_TOPMOST would sink below any
/// later top-most window. It is click-through and per-pixel transparent, so covering the full width
/// blocks nothing.</para>
///
/// <para>A persistent banner sits across the top (#266): while running it reads "MCEC is controlling
/// your PC", one line centered horizontally on the screen in the brand orange; while emergency-stopped
/// (#135) the red "⛔ STOPPED by operator" bar takes its place. The banner lives inside this window, so it
/// only appears when the overlay itself is enabled. The command feed is a running list below the banner
/// in its docked column: the newest command appears at the top and older ones are pushed down, filling
/// the screen height until the oldest scroll off the bottom edge. Entries persist (they do not time out);
/// the feed is bounded only by a line cap and the available screen height.</para>
/// </summary>
public sealed class CommandOverlayWindow : Form {
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;

    // The About box's brand orange (Color.FromArgb(192, 90, 36)) as the item background at ~30% alpha.
    private static readonly Color _itemBackground = Color.FromArgb(77, 192, 90, 36);

    // Emergency stop (#135): a solid, high-contrast red for the persistent STOPPED banner (mostly opaque
    // so it reads as an alarm, not a fading log line).
    private static readonly Color _stoppedBackground = Color.FromArgb(235, 176, 0, 0);

    // The persistent "being controlled" banner (#266): the brand orange, mostly opaque so it reads as a
    // steady status header rather than a fading log line.
    private static readonly Color _controlBackground = Color.FromArgb(225, 192, 90, 36);

    private readonly OverlayFeed _feed;
    private readonly Action<CommandEvent> _onEvent;
    private readonly Action<bool> _onEmergencyStop;
    // Periodic repaint that re-asserts top-most Z-order (see PushLayered): a window that goes top-most
    // AFTER the overlay would otherwise occlude it. It no longer ages the feed out; the feed is now a
    // persistent scrolling list (newest at top), redrawn on each new command event.
    private readonly System.Windows.Forms.Timer _repaintTimer;
    private readonly OverlayPosition _side;

    // Width of the docked command-feed column. The window spans the full screen width (so the banner can
    // center across the whole screen, #266), but the feed stays in this ~30% column on the docked edge.
    private readonly int _feedColumnWidth;

    // True while the operator's emergency stop is engaged; drives the persistent STOPPED banner.
    private bool _stopped;

    // The handle currently registered as ignored, tracked so a WinForms handle recreation never leaves a
    // stale HWND in the resolver's ignore set (a recycled value could otherwise hide a real window).
    private long _registeredHandle;

    public CommandOverlayWindow() {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Text = string.Empty;
        _side = AgentRuntime.Settings?.CommandOverlayPosition ?? OverlayPosition.Right;
        // The window spans the FULL screen (not the working area, so it sits over the taskbar too, and not
        // just a 30% side column): a full-width window is what lets the banner center across the whole
        // screen (#266). It is click-through and per-pixel transparent, so covering the width blocks
        // nothing. The command feed still hugs the docked edge inside a ~30% column (_feedColumnWidth).
        Rectangle bounds = OverlayLayout.ForSide(Screen.PrimaryScreen!.Bounds, 1.0, _side);
        Bounds = bounds;
        _feedColumnWidth = OverlayLayout.FeedColumnWidth(bounds.Width);
        // Cap the feed to fill the actual overlay height (the old fixed 8 left tall screens empty above
        // the last few lines); the renderer's geometric break still trims anything past the bottom edge.
        _feed = new OverlayFeed(maxLines: OverlayLayout.MaxLines(bounds.Height));

        _onEvent = OnCommandEvent;
        CommandEventHub.Subscribe(_onEvent);

        _stopped = EmergencyStop.IsStopped;
        _onEmergencyStop = OnEmergencyStopStateChanged;
        EmergencyStop.StateChanged += _onEmergencyStop;

_repaintTimer = new System.Windows.Forms.Timer { Interval = 300 };
_repaintTimer.Tick += (_, _) => {
    if (!IsHandleCreated || IsDisposed) {
        return;
    }
    AgentNativeMethods.SetWindowPos(Handle, AgentNativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
        AgentNativeMethods.SWP_NOMOVE | AgentNativeMethods.SWP_NOSIZE | AgentNativeMethods.SWP_NOACTIVATE);
};
_repaintTimer.Start();
    }

    private void OnEmergencyStopStateChanged(bool stopped) {
        if (IsDisposed || !IsHandleCreated) {
            return;
        }
        try {
            if (InvokeRequired) {
                BeginInvoke(_onEmergencyStop, stopped);
                return;
            }
            _stopped = stopped;
            Render();
        }
        catch (ObjectDisposedException) {
            // Window closed between the check and the marshal; nothing to draw.
        }
        catch (InvalidOperationException) {
            // Handle not ready; drop this update rather than throw on the hook thread.
        }
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
        // Defensively drop any previously-registered handle (e.g. if the window was recreated) before
        // registering the current one, so the ignore set never accumulates stale HWNDs.
        if (_registeredHandle != 0) {
            WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
        }
        _registeredHandle = Handle.ToInt64();
        WindowResolver.RegisterIgnoredWindow(_registeredHandle);
        Render();
    }

    protected override void OnHandleDestroyed(EventArgs e) {
        // Unregister as the handle is destroyed (not just on Dispose) so a recreated handle (or a value
        // Windows later reuses for a real window) is never left ignored.
        if (_registeredHandle != 0) {
            WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
            _registeredHandle = 0;
        }
        base.OnHandleDestroyed(e);
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
            _feed.Add(ev);
            Render();
        }
        catch (ObjectDisposedException) {
            // The window closed between the check and the marshal; nothing to draw.
        }
        catch (InvalidOperationException) {
            // Handle not ready; drop this line rather than risk throwing on the publisher's thread.
        }
    }

    /// <summary>Renders the feed to a 32bpp ARGB bitmap and pushes it to the layered window (per-pixel alpha).</summary>
    private void Render() {
        if (!IsHandleCreated || IsDisposed) {
            return;
        }
        int w = Math.Max(1, Bounds.Width);
        int h = Math.Max(1, Bounds.Height);
        using Bitmap bmp = new(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            // The emergency-stop banner takes precedence over the "being controlled" banner: while
            // stopped, the loud red alarm is what the operator needs to see, not the routine status.
            float bannerBottom = _stopped ? DrawStoppedBanner(g, w) : DrawControlBanner(g, w);
            DrawFeed(g, w, h, bannerBottom);
        }
        PushLayered(bmp);
    }

    private void DrawFeed(Graphics g, int width, int height, float top) {
        IReadOnlyList<CommandEvent> lines = _feed.Snapshot(); // newest first
        if (lines.Count == 0) {
            return;
        }

        using Font font = new("Consolas", 14F, FontStyle.Bold, GraphicsUnit.Point);
        const int pad = 8;
        const int gap = 6;

        // The feed lives in a ~30% column hugging the docked edge, even though the window spans the full
        // screen width (the full width exists only so the banner can center across the screen, #266).
        int colWidth = Math.Min(_feedColumnWidth, width);
        float colLeft = _side == OverlayPosition.Left ? 0f : width - colWidth;

        // Newest at the top, older pushed down: draw from just below the banner and walk toward the
        // bottom edge, so the most recent command is always the top box. When more lines exist than fit,
        // the oldest simply fall off the bottom (the geometric break below), no timeout involved.
        float y = top + gap;
        foreach (CommandEvent ev in lines) {
            SizeF textSize = g.MeasureString(ev.TerseText, font, colWidth - pad * 2);
            float boxH = textSize.Height + pad;
            if (y + boxH > height) {
                break; // ran out of room at the bottom edge; older lines fall off
            }
            float boxW = Math.Min(textSize.Width + pad * 2, colWidth);
            float boxX = _side == OverlayPosition.Left ? colLeft : colLeft + colWidth - boxW; // hug the docked edge

            using (SolidBrush scrim = new(_itemBackground))
            using (GraphicsPath path = RoundedRect(new RectangleF(boxX, y, boxW, boxH), 6f)) {
                g.FillPath(scrim, path);
            }

            Color fg = ev.Outcome switch {
                CommandOutcome.Failed => Color.FromArgb(255, 120, 120),
                CommandOutcome.Pending => Color.FromArgb(255, 214, 120),
                _ => Color.White,
            };
            float tx = boxX + pad;
            float ty = y + pad / 2f;
            using (SolidBrush shadow = new(Color.FromArgb(200, 0, 0, 0))) {
                g.DrawString(ev.TerseText, font, shadow, tx + 1.2f, ty + 1.2f);
            }
            using (SolidBrush brush = new(fg)) {
                g.DrawString(ev.TerseText, font, brush, tx, ty);
            }

            y += boxH + gap;
        }
    }

    /// <summary>
    /// Draws the persistent emergency-stop (#135) banner across the top of the overlay. Unlike the fading
    /// command feed, it stays until the operator re-arms; a loud, unmissable "MCEC is halted" indicator.
    /// Full width (a loud alarm bar). Returns the banner's bottom edge so the feed can start below it.
    /// </summary>
    private static float DrawStoppedBanner(Graphics g, int width) =>
        DrawBanner(g, width, "⛔ STOPPED by operator; Re-arm to resume", _stoppedBackground, centered: false);

    /// <summary>
    /// Draws the persistent "MCEC is controlling your PC" banner (#266): a single line, centered at the
    /// top, shown whenever the overlay is up and the stop is not engaged; a steady reminder to the human
    /// that MCEC is driving this machine. Returns the banner's bottom edge so the feed starts below it.
    /// </summary>
    private static float DrawControlBanner(Graphics g, int width) =>
        DrawBanner(g, width, OverlayLayout.ControlBannerText, _controlBackground, centered: true);

    /// <summary>
    /// Draws a top banner and returns its bottom edge (its height). <paramref name="centered"/> draws a
    /// pill hugging the text, horizontally centered; otherwise a full-width bar with left-aligned text.
    /// </summary>
    private static float DrawBanner(Graphics g, int width, string text, Color background, bool centered) {
        const int pad = 8;
        const float baseSize = 16F;
        // Auto-fit to the overlay width so the banner is always ONE un-clipped line. The overlay is a
        // narrow (~30% screen) docked column, so a fixed 16pt would clip on smaller screens; measure the
        // text unconstrained (its true single-line width) and shrink the font just enough to fit,
        // floored so it stays readable.
        using Font probe = new("Consolas", baseSize, FontStyle.Bold, GraphicsUnit.Point);
        float trueWidth = g.MeasureString(text, probe).Width;
        float maxTextWidth = width - pad * 2;
        float scale = trueWidth > maxTextWidth ? maxTextWidth / trueWidth : 1f;
        float fontSize = Math.Max(9F, baseSize * scale);
        using Font font = new("Consolas", fontSize, FontStyle.Bold, GraphicsUnit.Point);
        SizeF size = g.MeasureString(text, font);
        float boxH = size.Height + pad * 1.5f;
        float boxW = centered ? Math.Min(size.Width + pad * 2, width) : width;
        float boxX = centered ? (width - boxW) / 2f : 0f;
        float tx = boxX + (centered ? Math.Max(pad, (boxW - size.Width) / 2f) : pad);
        using (SolidBrush bg = new(background))
        using (GraphicsPath path = RoundedRect(new RectangleF(boxX, 0, boxW, boxH), 6f)) {
            g.FillPath(bg, path);
        }
        using (SolidBrush shadow = new(Color.FromArgb(200, 0, 0, 0))) {
            g.DrawString(text, font, shadow, tx + 1.2f, pad / 2f + 1.2f);
        }
        using (SolidBrush fg = new(Color.White)) {
            g.DrawString(text, font, fg, tx, pad / 2f);
        }
        return boxH;
    }

    /// <summary>Pushes a 32bpp ARGB bitmap to this layered window so its per-pixel alpha composites over the desktop.</summary>
    private void PushLayered(Bitmap bmp) {
        IntPtr screenDc = AgentNativeMethods.GetDC(IntPtr.Zero);
        IntPtr memDc = AgentNativeMethods.CreateCompatibleDC(screenDc);
        IntPtr hBmp = IntPtr.Zero;
        IntPtr oldBmp = IntPtr.Zero;
        try {
            hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            oldBmp = AgentNativeMethods.SelectObject(memDc, hBmp);

            Point dst = new(Bounds.Left, Bounds.Top);
            Point src = new(0, 0);
            Size size = new(bmp.Width, bmp.Height);
            BlendFunction blend = new() {
                BlendOp = AgentNativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255, // use only the bitmap's per-pixel alpha, no extra dimming
                AlphaFormat = AgentNativeMethods.AC_SRC_ALPHA,
            };
            AgentNativeMethods.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, AgentNativeMethods.ULW_ALPHA);

            // Re-assert top-most on every paint. WS_EX_TOPMOST only puts the window in the always-on-top
            // band at creation; any window that goes top-most LATER lands above it. Bumping to
            // HWND_TOPMOST here (driven by the 300ms age timer) keeps the overlay at the top of that band
            // so it is not occluded. NOMOVE|NOSIZE preserve the layered geometry; NOACTIVATE keeps it
            // click-through and never steals focus from the app being driven.
            AgentNativeMethods.SetWindowPos(Handle, AgentNativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                AgentNativeMethods.SWP_NOMOVE | AgentNativeMethods.SWP_NOSIZE | AgentNativeMethods.SWP_NOACTIVATE);
        }
        finally {
            if (oldBmp != IntPtr.Zero) {
                AgentNativeMethods.SelectObject(memDc, oldBmp);
            }
            if (hBmp != IntPtr.Zero) {
                AgentNativeMethods.DeleteObject(hBmp);
            }
            AgentNativeMethods.DeleteDC(memDc);
            _ = AgentNativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
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
            EmergencyStop.StateChanged -= _onEmergencyStop;
            _repaintTimer.Dispose();
            // OnHandleDestroyed normally clears the registration; unregister defensively in case the
            // window is disposed without a handle-destroyed notification.
            if (_registeredHandle != 0) {
                WindowResolver.UnregisterIgnoredWindow(_registeredHandle);
                _registeredHandle = 0;
            }
        }
        base.Dispose(disposing);
    }
}
