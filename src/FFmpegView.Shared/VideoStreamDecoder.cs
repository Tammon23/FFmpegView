﻿using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static FFmpegView.IMedia;

namespace FFmpegView
{
    public sealed unsafe class VideoStreamDecoder : IMedia, INotifyPropertyChanged
    {
        int error;
        private long _curTimeStamp;
        private long _curTimeStampOld = -1;

        public TimeSpan Test = TimeSpan.Zero;
        
        public TimeSpan CurTimeStamp => TimeSpan.FromSeconds(_curTimeStamp);

        private long StartTime;
        private long StartRealTime;
        double timeBase;
        AVFormatContext* format;
        AVCodecContext* codecContext;
        AVPacket* packet;
        AVFrame* frame;
        SwsContext* convert;
        AVStream* videoStream;
        int videoStreamIndex;
        TimeSpan OffsetClock;
        IntPtr FrameBufferPtr;
        byte_ptrArray4 TargetData;
        int_array4 TargetLinesize;
        public readonly Stopwatch clock = new();
        readonly object SyncLock = new();
        TimeSpan lastTime;
        bool isNextFrame = true;
        public event MediaHandler MediaPlay;
        public event MediaHandler MediaPause;
        public event MediaHandler MediaCompleted;
        public event MediaMsgHandler MediaMsgRecevice;
        public Dictionary<string, string> Headers { get; set; }
        #region
        public TimeSpan Duration { get; private set; }
        public string CodecName { get; private set; }
        public string CodecId { get; private set; }
        public int Bitrate { get; private set; }
        public double FrameRate { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public bool IsPlaying { get; private set; }
        public MediaState State { get; private set; }
        public TimeSpan Position => clock.Elapsed + OffsetClock;
        
        public TimeSpan frameDuration { get; private set; }
        #endregion
        public VideoStreamDecoder()
        {
            Headers = new Dictionary<string, string>();
        }
        public void InitDecodecVideo(string uri)
        {
            try
            {
                format = ffmpeg.avformat_alloc_context();
                if (format == null)
                {
                    SendMsg(MsgType.Information, "Failed to create media format (container)");
                    return;
                }
                var tempFormat = format;
                AVDictionary* options = Headers.ToHeader();
                error = ffmpeg.avformat_open_input(&tempFormat, uri, null, &options);
                if (error < 0)
                {
                    SendMsg(MsgType.Information, "Failed to open video");
                    return;
                }
                ffmpeg.avformat_find_stream_info(format, null);
                AVCodec* codec = null;
                videoStreamIndex = ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0);
                if (videoStreamIndex < 0)
                {
                    SendMsg(MsgType.Information, "No video stream found");
                    return;
                }
                videoStream = format->streams[videoStreamIndex];
                StartTime       = format->start_time != ffmpeg.AV_NOPTS_VALUE ? format->start_time * 10 : 0;
                StartRealTime   = format->start_time_realtime != ffmpeg.AV_NOPTS_VALUE ? format->start_time_realtime * 10 : 0;
                
                timeBase = ffmpeg.av_q2d(videoStream->time_base);
                codecContext = ffmpeg.avcodec_alloc_context3(codec);
                error = ffmpeg.avcodec_parameters_to_context(codecContext, videoStream->codecpar);
                if (error < 0)
                {
                    SendMsg(MsgType.Information, "Failed to set decoder parameters");
                    return;
                }
                error = ffmpeg.avcodec_open2(codecContext, codec, null);
                if (error < 0)
                {
                    SendMsg(MsgType.Information, "Failed to open decoder");
                    return;
                }
                Duration = TimeSpan.FromMilliseconds(format->duration / 1000);
                CodecId = videoStream->codecpar->codec_id.ToString();
                CodecName = ffmpeg.avcodec_get_name(videoStream->codecpar->codec_id);
                Bitrate = (int)videoStream->codecpar->bit_rate;
                FrameRate = ffmpeg.av_q2d(videoStream->r_frame_rate);
                FrameWidth = videoStream->codecpar->width;
                FrameHeight = videoStream->codecpar->height;
                frameDuration = TimeSpan.FromMilliseconds(1000 / FrameRate);
                var result = InitConvert(FrameWidth, FrameHeight, codecContext->pix_fmt, FrameWidth, FrameHeight, AVPixelFormat.AV_PIX_FMT_BGR0);
                if (result)
                {
                    packet = ffmpeg.av_packet_alloc();
                    frame = ffmpeg.av_frame_alloc();
                }
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, $"FFmpeg InitDecodecVideo Failed: {ex.Message}");
            }
        }
        private void SendMsg(MsgType type, string msg) => MediaMsgRecevice?.Invoke(type, msg);
        private bool InitConvert(int sourceWidth, int sourceHeight, AVPixelFormat sourceFormat, int targetWidth, int targetHeight, AVPixelFormat targetFormat)
        {
            try
            {
                convert = ffmpeg.sws_getContext(sourceWidth, sourceHeight, sourceFormat, targetWidth, targetHeight, targetFormat, ffmpeg.SWS_FAST_BILINEAR, null, null, null);
                if (convert == null)
                {
                    SendMsg(MsgType.Information, "Failed to create converter");
                    return false;
                }
                var bufferSize = ffmpeg.av_image_get_buffer_size(targetFormat, targetWidth, targetHeight, 1);
                FrameBufferPtr = Marshal.AllocHGlobal(bufferSize);
                TargetData = new byte_ptrArray4();
                TargetLinesize = new int_array4();
                ffmpeg.av_image_fill_arrays(ref TargetData, ref TargetLinesize, (byte*)FrameBufferPtr, targetFormat, targetWidth, targetHeight, 1);
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Error, $"FFmpeg InitConvert Failed: {ex.Message}");
                return false;
            }
        }
        public AVFrame FrameConvert(AVFrame* sourceFrame)
        {
            ffmpeg.sws_scale(convert, sourceFrame->data, sourceFrame->linesize, 0, sourceFrame->height, TargetData, TargetLinesize);
            var data = new byte_ptrArray8();
            data.UpdateFrom(TargetData);
            var linesize = new int_array8();
            linesize.UpdateFrom(TargetLinesize);
            return new AVFrame
            {
                data = data,
                linesize = linesize,
                width = FrameWidth,
                height = FrameHeight
            };
        }
        public bool TryReadNextFrame(out AVFrame outFrame)
        {
            try
            {
                if (lastTime == TimeSpan.Zero)
                {
                    lastTime = Position;
                    isNextFrame = true;
                }
                else
                {
                    if (Position - lastTime >= frameDuration)
                    {
                        lastTime = Position;
                        isNextFrame = true;
                    }
                    else
                    {
                        outFrame = *frame;
                        return false;
                    }
                }
                if (isNextFrame && error >= 0)
                {
                    lock (SyncLock)
                    {
                        var result = -1;
                        ffmpeg.av_frame_unref(frame);
                        while (true)
                        {
                            ffmpeg.av_packet_unref(packet);
                            result = ffmpeg.av_read_frame(format, packet);
                            if (result == ffmpeg.AVERROR_EOF || result < 0)
                            {
                                outFrame = *frame;
                                StopPlay();
                                return false;
                            }
                            if (packet->stream_index != videoStreamIndex)
                                continue;
                            ffmpeg.avcodec_send_packet(codecContext, packet);
                            result = ffmpeg.avcodec_receive_frame(codecContext, frame);
                            if (result < 0) continue;
                            outFrame = *frame;
                            
                            // calculating the current timestamp
                            var timestamp = (long)(packet->pts * timeBase);
                            _curTimeStamp = timestamp - StartTime;

                            UpdateCurTimeStamp();
                            
                            return true;
                        }
                    }
                }
                else
                {
                    outFrame = *frame;
                    return false;
                }
            }
            catch (Exception ex)
            {
                outFrame = *frame;
                SendMsg(MsgType.Error, $"FFmpeg TryReadNextFrame Failed: {ex.Message}");
                return false;
            }
        }
        private void StopPlay()
        {
            try
            {
                lock (SyncLock)
                {
                    if (State == MediaState.None) return;
                    IsPlaying = false;
                    OffsetClock = TimeSpan.FromSeconds(0);
                    clock.Reset();
                    var tempFormat = format;
                    ffmpeg.avformat_free_context(tempFormat);
                    format = null;
                    var tempCodecContext = codecContext;
                    ffmpeg.avcodec_free_context(&tempCodecContext);
                    var tempPacket = packet;
                    ffmpeg.av_packet_free(&tempPacket);
                    var tempFrame = frame;
                    ffmpeg.av_frame_free(&tempFrame);
                    var tempConvert = convert;
                    ffmpeg.sws_freeContext(convert);
                    videoStream = null;
                    error = -1;
                    videoStreamIndex = -1;
                    Duration = TimeSpan.FromMilliseconds(0);
                    CodecName = string.Empty;
                    CodecId = string.Empty;
                    Bitrate = 0;
                    FrameRate = 0;
                    FrameWidth = 0;
                    FrameHeight = 0;
                    State = MediaState.None;
                    Marshal.FreeHGlobal(FrameBufferPtr);
                    lastTime = TimeSpan.Zero;
                    MediaCompleted?.Invoke(Duration);
                }
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Information, $"FFmpeg : Failed to stop ({ex.Message})");
            }
        }
        public bool SeekProgress(double seekTime)
        {
            try
            {
                if (format == null || videoStream == null || error < 0)
                    return false;
                lock (SyncLock)
                {
                    IsPlaying = false;
                    clock.Stop();

                    var timestamp = ffmpeg.av_rescale_q((long)(seekTime * ffmpeg.AV_TIME_BASE), ffmpeg.av_get_time_base_q(), videoStream->time_base);

                    ffmpeg.av_seek_frame(format, videoStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_FRAME);
                    ffmpeg.av_frame_unref(frame);
                    ffmpeg.av_packet_unref(packet);
                    int error;
                    receiveFrame();
                    void receiveFrame()
                    {
                        while (packet->pts < timestamp)
                        {
                            do
                            {
                                do
                                {
                                    ffmpeg.av_packet_unref(packet);
                                    error = ffmpeg.av_read_frame(format, packet);
                                    if (error == ffmpeg.AVERROR_EOF)
                                        return;
                                } while (packet->stream_index != videoStreamIndex);
                                ffmpeg.avcodec_send_packet(codecContext, packet);
                                error = ffmpeg.avcodec_receive_frame(codecContext, frame);
                            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));
                        }
                    }
                    OffsetClock = TimeSpan.FromSeconds(seekTime);
                    clock.Restart();
                    IsPlaying = true;
                    lastTime = TimeSpan.Zero;
                    
                    // calculating the current timestamp
                    var new_timestamp = (long)(packet->pts * timeBase);
                    _curTimeStamp = new_timestamp - StartTime;
                    
                    UpdateCurTimeStamp();
                }
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Information, $"FFmpeg : Failed to seek({ex.Message})");
                return false;
            }
        }
        public bool Play()
        {
            try
            {
                if (!Core.IsInitialize)
                {
                    SendMsg(MsgType.Information, "FFmpeg : dosnot initialize device");
                    return false;
                }
                if (State != MediaState.Play)
                {
                    clock.Start();
                    IsPlaying = true;
                    State = MediaState.Play;
                    MediaPlay?.Invoke(Duration);
                }
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Information, $"FFmpeg : Failed to play({ex.Message})");
                return false;
            }
        }
        public bool Pause()
        {
            try
            {
                if (State == MediaState.Play)
                {
                    IsPlaying = false;
                    OffsetClock = clock.Elapsed;
                    clock.Reset();
                    State = MediaState.Pause;
                    MediaPause?.Invoke(Duration);
                }
                return true;
            }
            catch (Exception ex)
            {
                SendMsg(MsgType.Information, $"FFmpeg : Failed to pause({ex.Message})");
                return false;
            }
        }
        public void Stop()
        {
            if (State == MediaState.None)
                return;
            StopPlay();
        }

        private void UpdateCurTimeStamp()
        {
            if (_curTimeStamp < 0)
                _curTimeStamp = 0;
            
            if (_curTimeStampOld == _curTimeStamp) 
                return;
            
            _curTimeStampOld = _curTimeStamp;
            OnPropertyChanged(nameof(CurTimeStamp));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}