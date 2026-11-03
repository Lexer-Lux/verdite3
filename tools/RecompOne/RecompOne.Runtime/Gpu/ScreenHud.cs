using System.Threading;
using Silk.NET.OpenGL;

namespace RecompOne.Runtime;

/// <summary>A frame-local canvas in physical framebuffer pixels, with a top-left
/// origin. It covers the whole host window, including letterbox margins. Use it
/// only during the registered callback; it never reads or changes game memory.</summary>
public interface IScreenHudCanvas
{
    int Width { get; }
    int Height { get; }
    /// <summary>Replace a clipped rectangle's colour. The packed value is
    /// 0xRRGGBBAA; alpha is stored, not blended. Non-positive sizes draw nothing.</summary>
    void FillRect(int x, int y, int width, int height, uint rgba);
}

/// <summary>Bounded, opt-in HUD drawing after the composed game and host UI.
/// Gameplay decisions and data belong to the registering mod.</summary>
public static class ScreenHud
{
    public const int MaximumCallbacks = 32;
    static readonly object Gate = new();
    static Registration[] _callbacks = [];

    public static IDisposable Register(string owner, Action<IScreenHudCanvas> draw)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128 || owner.Any(char.IsControl))
            throw new ArgumentException("A HUD owner needs 1..128 printable characters", nameof(owner));
        ArgumentNullException.ThrowIfNull(draw);
        lock (Gate)
        {
            if (_callbacks.Any(entry => entry.Owner == owner))
                throw new InvalidOperationException($"HUD owner {owner} is already registered");
            if (_callbacks.Length >= MaximumCallbacks)
                throw new InvalidOperationException("The screen HUD callback limit has been reached");
            var entry = new Registration(owner, draw);
            Volatile.Write(ref _callbacks, [.. _callbacks, entry]);
            return entry;
        }
    }

    sealed class Registration(string owner, Action<IScreenHudCanvas> draw) : IDisposable
    {
        internal readonly string Owner = owner;
        internal readonly Action<IScreenHudCanvas> Draw = draw;
        internal int Removed;
        public void Dispose() => Remove();
        internal bool Remove()
        {
            if (Interlocked.Exchange(ref Removed, 1) != 0) return false;
            lock (Gate)
                Volatile.Write(ref _callbacks, _callbacks.Where(entry => entry != this).ToArray());
            return true;
        }
    }

    static void Dispatch(IScreenHudCanvas canvas)
    {
        foreach (var entry in Volatile.Read(ref _callbacks))
        {
            if (Volatile.Read(ref entry.Removed) != 0) continue;
            try { entry.Draw(canvas); }
            catch (Exception error)
            {
                if (entry.Remove())
                    Console.Error.WriteLine($"[ScreenHud] {entry.Owner} disabled: {error.GetType().Name}: {error.Message}");
            }
        }
    }

    /// <summary>Host render thread only, after ImGui has composed the default
    /// framebuffer. Save exactly the GL state the rectangle primitive changes.</summary>
    internal static unsafe void Render(GL gl, int width, int height)
    {
        if (width <= 0 || height <= 0 || Volatile.Read(ref _callbacks).Length == 0) return;
        string version = gl.GetStringS(StringName.Version) ?? "";
        int dot = version.IndexOf('.');
        bool core3 = dot > 0 && int.TryParse(version.AsSpan(0, dot), out int major) && major >= 3;
        bool drawTarget = core3 || gl.IsExtensionPresent("GL_ARB_framebuffer_object")
                                || gl.IsExtensionPresent("GL_EXT_framebuffer_blit");
        bool indexedMask = core3 || gl.IsExtensionPresent("GL_EXT_draw_buffers2");
        var target = drawTarget ? FramebufferTarget.DrawFramebuffer : FramebufferTarget.Framebuffer;
        uint framebuffer = (uint)gl.GetInteger(GLEnum.FramebufferBinding);
        bool scissorEnabled = gl.IsEnabled(EnableCap.ScissorTest);
        int* scissor = stackalloc int[4];
        int* mask = stackalloc int[4];
        float* clear = stackalloc float[4];
        gl.GetInteger(GLEnum.ScissorBox, scissor);
        gl.GetInteger(GLEnum.ColorWritemask, mask);
        gl.GetFloat(GLEnum.ColorClearValue, clear);
        var canvas = new Canvas(gl, width, height);
        try
        {
            gl.BindFramebuffer(target, 0);
            gl.Enable(EnableCap.ScissorTest);
            if (indexedMask) gl.ColorMask(0, true, true, true, true);
            else gl.ColorMask(true, true, true, true);
            Dispatch(canvas);
        }
        finally
        {
            canvas.Active = false;
            gl.ClearColor(clear[0], clear[1], clear[2], clear[3]);
            if (indexedMask) gl.ColorMask(0, mask[0] != 0, mask[1] != 0, mask[2] != 0, mask[3] != 0);
            else gl.ColorMask(mask[0] != 0, mask[1] != 0, mask[2] != 0, mask[3] != 0);
            gl.Scissor(scissor[0], scissor[1], (uint)scissor[2], (uint)scissor[3]);
            if (!scissorEnabled) gl.Disable(EnableCap.ScissorTest);
            gl.BindFramebuffer(target, framebuffer);
        }
    }

    sealed class Canvas(GL gl, int width, int height) : IScreenHudCanvas
    {
        readonly int _thread = Environment.CurrentManagedThreadId;
        internal bool Active = true;
        public int Width => width;
        public int Height => height;
        public void FillRect(int x, int y, int w, int h, uint rgba)
        {
            if (!Active || Environment.CurrentManagedThreadId != _thread)
                throw new InvalidOperationException("Use the HUD canvas only inside its render callback");
            if (w <= 0 || h <= 0) return;
            // Widen before adding: caller coordinates may lie at either int limit.
            long left = Math.Max(0L, x), top = Math.Max(0L, y);
            long right = Math.Min((long)width, (long)x + w), bottom = Math.Min((long)height, (long)y + h);
            if (right <= left || bottom <= top) return;
            gl.Scissor((int)left, (int)(height - bottom), (uint)(right - left), (uint)(bottom - top));
            gl.ClearColor((rgba >> 24) / 255f, ((rgba >> 16) & 255) / 255f,
                          ((rgba >> 8) & 255) / 255f, (rgba & 255) / 255f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
        }
    }
}
