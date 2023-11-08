using NAudio.Wave;

namespace FFmpegView.NAudio
{
    public unsafe class NAudioStreamDecoder : AudioStreamDecoder
    {
        private readonly WaveOut waveOut;
        private readonly BufferedWaveProvider bufferedWaveProvider;
        public NAudioStreamDecoder()
        {
            waveOut = new WaveOut();
            bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat());
            waveOut.Init(bufferedWaveProvider);
            waveOut.Play();
        }
        public override void Prepare() { }
        public override double GetVolumeCore()
        {
            throw new System.NotImplementedException();
        }

        public override void PlayNextFrame(byte[] bytes)
        {
            if (bufferedWaveProvider.BufferLength <= bufferedWaveProvider.BufferedBytes + bytes.Length)
                bufferedWaveProvider.ClearBuffer();
            bufferedWaveProvider.AddSamples(bytes, 0, bytes.Length);
        }
        public override void StopCore() { }
        public override void PauseCore() { }
        public override void UnPauseCore() { }

        public override void MuteCore() { }

        public override void UnMuteCore() { }
        public override void SetVolumeCore(double volume) { }
    }
}