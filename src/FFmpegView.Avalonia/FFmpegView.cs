using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCLUntils.Objects;
using System;
#if NET40_OR_GREATER
using System.Runtime.ExceptionServices;
using System.Security;
#endif
using System.Threading;
using System.Threading.Tasks;
using Avalonia.VisualTree;

namespace FFmpegView;

[PseudoClasses(":empty")]
[TemplatePart("PART_ContentControlView", typeof(ContentControl))]
public unsafe class FFmpegView : ContentControl
{
    private ContentControl _contentControl;
    private Task _playTask;
    private bool _isAttached;
    private CancellationTokenSource _cancellationToken;
    private Player _mediaPlayer;
    private ImageBrush _imageBrush;

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<FFmpegView, Stretch>(nameof(Stretch), Stretch.Uniform);
    
    /// <summary>
    /// Gets or sets a value controlling how the video will be stretched.
    /// </summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    
    public static readonly DirectProperty<FFmpegView, Player> MediaPlayerProperty = AvaloniaProperty.RegisterDirect<FFmpegView, Player>(
        "MediaPlayer", o => o.MediaPlayer, (o, v) => o.MediaPlayer = v);

    public Player MediaPlayer
    {
        get => _mediaPlayer;
        set
        {
            SetAndRaise(MediaPlayerProperty, ref _mediaPlayer, value);
            
            _isAttached = this.IsAttachedToVisualTree();
            _cancellationToken = new CancellationTokenSource();
            _playTask = new Task(DrawImage, _cancellationToken.Token);
            _playTask.Start();
            
            MediaPlayer.Init(_cancellationToken);
        }
    }
    
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        try
        {
            _cancellationToken.Cancel();
            _playTask.Dispose();
            MediaPlayer.AudioTask.Dispose();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        base.OnAttachedToVisualTree(e);
        
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _contentControl = e.NameScope.Get<ContentControl>("PART_ContentControlView");
    }
    
#if NET40_OR_GREATER
                [SecurityCritical]
                [HandleProcessCorruptedStateExceptions]
#endif
    private void DrawImage()
    {
        while (MediaPlayer.IsRunning)
        {
            try
            {
                if (MediaPlayer.Video.IsPlaying && _isAttached)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (MediaPlayer.Video.TryReadNextFrame(out var frame))
                        {
                            var convertedFrame = MediaPlayer.Video.FrameConvert(&frame);
                            _imageBrush = new ImageBrush(
                                new Bitmap(
                                    PixelFormat.Bgra8888,
                                    AlphaFormat.Premul,
                                    (IntPtr) convertedFrame.data[0],
                                    new PixelSize(MediaPlayer.Video.FrameWidth, MediaPlayer.Video.FrameHeight),
                                    new Vector(96, 96),
                                    convertedFrame.linesize[0]
                                )
                            );
                        }
                        if (_contentControl.IsNotEmpty())
                            _contentControl.Background = _imageBrush;
                    });
                    
  
                }
                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            }
        }
    }
}