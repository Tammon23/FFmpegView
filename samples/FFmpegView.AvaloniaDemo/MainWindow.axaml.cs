using System;
using Avalonia.Controls;
using FFmpegView.Bass;
using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace FFmpegView.AvaloniaDemo
{
    public partial class MainWindow : Window
{
    public Player MediaPlayer { get; } = new Player();

    public MainWindow()
    {
        InitializeComponent();
        InitPlayer();
        playerView.MediaPlayer = MediaPlayer;

        BtnStop.Command = ReactiveCommand.Create(() =>
        {
            MediaPlayer.Stop();
            TextBlockDuration.Text = "00:00:00";

        });
        
        // if is play
        BtnPlayPause.Command = ReactiveCommand.Create(() =>
        {
            if (MediaPlayer.IsPlaying())
            {
                MediaPlayer.Pause();
                BtnPlayPause.Content = "▶";
            }
            else
            {
                BtnPlayPause.Content = "⏸";
                if (MediaPlayer.HasMedia())
                {
                    // unpause instead of start a new
                    MediaPlayer.Play();
                    
                }
                else
                {
                    MediaPlayer.Stop();
                    MediaPlayer.Play("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
                    TextBlockDuration.Text = MediaPlayer.Duration.ToString(@"hh\:mm\:ss");
                }
            }
 
        });
        
        BtnMuteUnMute.Command = ReactiveCommand.Create(() =>
        {
            if (MediaPlayer.IsMuted())
            {
                MediaPlayer.UnMute();
                BtnMuteUnMute.Content = "🔊";
            }
            else
            {
                MediaPlayer.Mute();
                BtnMuteUnMute.Content = "🔇";
            }
        });

        MediaPlayer.WhenAnyValue(x => x.CurTime)
            .BindTo(this, x => x.TextBlockCurrentTimeStamp.Text);

        MediaPlayer.WhenAnyValue(x => x.CurTime)
            .Select(x => x / MediaPlayer.Duration * 100)
            .BindTo(this, x => x.SliderVideoProgression.Value);

        // moving the progress bar will seek to that position in the media
        SliderVideoProgression.PointerCaptureLost += (_, args) =>
        {
            var newValueConverted = SliderVideoProgression.Value / 100 * MediaPlayer.Duration;
            MediaPlayer.CurTime = newValueConverted;
            args.Handled = true;
        };

        
        SliderVolume.Value = MediaPlayer.GetVolume();
        SliderVolume
            .GetObservable(RangeBase.ValueProperty)
            .Subscribe(x => MediaPlayer.SetVolume(x));

        BtnFullScreen.Command = ReactiveCommand.Create(() =>
        {
            WindowState = WindowState != WindowState.FullScreen ? WindowState.FullScreen : WindowState.Normal;
        });
    }

    private void InitPlayer()
    {
        var audioStreamDecoder = new BassAudioStreamDecoder();
        audioStreamDecoder.Headers = new Dictionary<string, string> { { "User-Agent", "ffmpeg_demo" } };
        MediaPlayer.SetAudioHandler(audioStreamDecoder);
    }
}
}

