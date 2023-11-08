using Avalonia.Logging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET40_OR_GREATER
using System.Runtime.ExceptionServices;
using System.Security;
#endif
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;


namespace FFmpegView;

public class Player: IFFmpegView, INotifyPropertyChanged
{
    private AudioStreamDecoder _audio;
    private readonly TimeSpan _timeout;
    private bool _isInit;
    private string? _media = null;
    
    public Task AudioTask;
    public bool IsRunning = true;
    public readonly VideoStreamDecoder Video;

    public void SetAudioHandler(AudioStreamDecoder decoder) => _audio = decoder;
    public void SetHeader(Dictionary<string, string> headers) => Video.Headers = headers;

    public Player()
    {
        Video = new VideoStreamDecoder
        {
            Headers = new Dictionary<string, string> { { "User-Agent", "ffmpeg_demo" } }
        };
        _timeout = TimeSpan.FromTicks(10000);
        Video.MediaCompleted += VideoMediaCompleted;
        Video.MediaMsgRecevice += Video_MediaMsgRecevice;

        Video.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(Video.CurTimeStamp):
                    OnPropertyChanged(nameof(CurTime));
                    break;
            }
        };
    }
    
    private void Video_MediaMsgRecevice(MsgType type, string msg)
    {
        if (type == MsgType.Error)
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, msg);
        else
            Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, msg);
    }

    private void VideoMediaCompleted(TimeSpan duration) =>
        Dispatcher.UIThread.InvokeAsync(DisplayVideoInfo);
    
    public bool Play()
    {
        bool state = false;
        try
        {
            state = Video.Play();
            var res = _audio.Play();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }
        return state;
    }
    public bool Play(MediaItem media)
    {
        if (!_isInit)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "FFmpeg : dosnot initialize device");
            return false;
        }
        bool state = false;
        try
        {
            if (Video.State == MediaState.None)
            {
                Video.Headers = media.Headers;
                Video.InitDecodecVideo(media.VideoUrl);
                _audio?.InitDecodecAudio(media.AudioUrl);
                _audio?.Prepare();
                DisplayVideoInfo();
            }
            state = Video.Play();
            _audio?.Play();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }
        return state;
    }
    public bool Play(string uri, Dictionary<string, string> headers = null)
    {
        if (!_isInit)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "FFmpeg : dosnot initialize device");
            return false;
        }
        bool state = false;
        try
        {
            _media = uri;
            if (Video.State == MediaState.None)
            {
                Video.Headers = headers;
                Video.InitDecodecVideo(uri);
                _audio?.InitDecodecAudio(uri);
                _audio?.Prepare();
                DisplayVideoInfo();
            }
            state = Video.Play();
            _audio?.Play();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }
        return state;
    }
    public bool SeekTo(int seekTime)
    {
        try
        {
            Pause();
            _audio?.SeekProgress(seekTime);
            return Video.SeekProgress(seekTime);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            return false;
        }
    }
    public bool Pause()
    {
        try
        {
            Debug.WriteLine("Pause Pressed");
            _audio.Pause(); //for some reason breaks sound when seeking
            return Video.Pause();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            return false;
        }
    }
    public bool Stop()
    {
        try
        {
            _audio?.Stop();
            Video.Stop();
            _media = null;
            return true;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Used to mute the media. If the media is already muted nothing will happen
    /// </summary>
    public void Mute()
    {
        try
        {
            _audio.Mute();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }

    }
    /// <summary>
    /// Used to unmute the media. If the media is already unmuted nothing will happen
    /// </summary>
    public void UnMute()
    {
        try
        {
            _audio.UnMute();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }

    }
    
    /// <summary>
    /// Used to set the volume of the media being played
    /// </summary>
    /// <param name="volume">0 (low) -> 1 (high)</param>
    public void SetVolume(double volume)
    {
        try
        {
            _audio.SetVolume(volume);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }
    }
    
    /// <summary>
    /// Used to get the volume of the media being played
    /// </summary>
    /// <returns>double between 0 (low) -> 1 (high)</returns>
    public double GetVolume()
    {
        try
        {
            return _audio.GetVolume();
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }

        return 0.5;
    }
    
    /// <summary>
    /// Used to see if the media is currently muted
    /// </summary>
    /// <returns>true if muted false if not</returns>
    public bool IsMuted()
    {
        try
        {
            return _audio.IsMuted;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }

        return false;
    }
    
    /// <summary>
    /// Used to see if the media is currently playing
    /// </summary>
    /// <returns>true if playing false if not</returns>
    public bool IsPlaying()
    {
        try
        {
            return Video.IsPlaying;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
        }

        return false;
    }

    public bool HasMedia()
    {
        return _media != null;
    }

    public TimeSpan CurTime
    {
        get => Video.CurTimeStamp;
        set => SeekTo(Convert.ToInt32(Math.Floor(value.TotalSeconds)));
    }

    public void Dispo() => IsRunning = false;
    public void Init(CancellationTokenSource cancellationToken)
    {
        try
        {
            AudioTask = new Task(() =>
            {
                while (IsRunning)
                {
                    try
                    {
                        if (_audio?.IsPlaying == true)
                        {
                            if (_audio?.TryPlayNextFrame() == true)
                                Thread.Sleep(_audio.frameDuration.Subtract(_timeout));
                        }
                        else
                            Thread.Sleep(10);
                    }
                    catch (Exception ex)
                    {
                        Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, ex.Message);
                    }
                }
            }, cancellationToken.Token);
            AudioTask.Start();
            _isInit = true;
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "FFmpeg Failed Init: " + ex.Message);
            _isInit = false;
        }
    }
    
    #region 视频信息
    private string _codec;
    public string Codec => _codec;
    private TimeSpan _duration;
    public TimeSpan Duration => _duration;
    private double _videoFps;
    public double VideoFps => _videoFps;
    private double _frameHeight;
    public double FrameHeight => _frameHeight;
    private double _frameWidth;
    public double FrameWidth => _frameWidth;
    private int _videoBitrate;
    public int VideoBitrate => _videoBitrate;
    private double _sampleRate;
    public double SampleRate => _sampleRate;
    private long _audioBitrate;
    public long AudioBitrate => _audioBitrate;
    private long _audioBitsPerSample;
    public long AudioBitsPerSample => _audioBitsPerSample;

    void DisplayVideoInfo()
    {
        try
        {
            _duration = Video.Duration;
            _codec = Video.CodecName;
            _videoBitrate = Video.Bitrate;
            _frameWidth = Video.FrameWidth;
            _frameHeight = Video.FrameHeight;
            _videoFps = Video.FrameRate;
            if (_audio != null)
            {
                _audioBitrate = _audio.Bitrate;
                _sampleRate = _audio.SampleRate;
                _audioBitsPerSample = _audio.BitsPerSample;
            }
        }
        catch { }
    }
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}