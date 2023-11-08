using ManagedBass;

namespace FFmpegView.Bass
{
    public class BassAudioStreamDecoder : AudioStreamDecoder
    {
        private Errors error;
        private int decodeStream;
        public Errors LastError => error;
        private double _oldVolume = 0.5;
        public override void PauseCore()
        {
            ManagedBass.Bass.ChannelPause(decodeStream);
        }

        public override void UnPauseCore()
        {
            ManagedBass.Bass.ChannelPlay(decodeStream);
        }

        public override void MuteCore()
        {
            _oldVolume = ManagedBass.Bass.ChannelGetAttribute(decodeStream, ChannelAttribute.Volume);
            ManagedBass.Bass.ChannelSetAttribute(decodeStream, ChannelAttribute.Volume, 0);
        }

        public override void UnMuteCore()
        {
            ManagedBass.Bass.ChannelSetAttribute(decodeStream, ChannelAttribute.Volume, _oldVolume);
        }

        public override void SetVolumeCore(double volume)
        {
            ManagedBass.Bass.ChannelSetAttribute(decodeStream, ChannelAttribute.Volume, volume);
        }
        
        public override double GetVolumeCore()
        {
            return ManagedBass.Bass.ChannelGetAttribute(decodeStream, ChannelAttribute.Volume);
        }

        public override void StopCore()
        {
            ManagedBass.Bass.ChannelStop(decodeStream);
        }
        public override void Prepare()
        {
            if (decodeStream != 0)
                ManagedBass.Bass.StreamFree(decodeStream);
            decodeStream = ManagedBass.Bass.CreateStream(SampleRate, Channels, BassFlags.Mono, StreamProcedureType.Push);
            if (!ManagedBass.Bass.ChannelPlay(decodeStream, true))
                error = ManagedBass.Bass.LastError;
        }
        public override void PlayNextFrame(byte[] bytes)
        {
            if (ManagedBass.Bass.StreamPutData(decodeStream, bytes, bytes.Length) == -1)
                error = ManagedBass.Bass.LastError;
        }
    }
}