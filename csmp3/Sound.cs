using System;
using System.IO;
/**
 * A sound object, that represents an input stream of uncompressed PCM sound data samples, decoded from encoded MPEG data.
 * <p>
 * To create a sound object from encoded MPEG data (MP1/MP2/MP3), simply use {@link #Sound(Stream)}. The decoding process will be done as data is read from this stream. You may also, as a convenience, write all the (remaining) decoded data into an {@link OutputStream} using {@link #decodeFullyInto(OutputStream)}.
 * <p>
 * You may use the several metadata functions such as {@link #getSamplingFrequency()} to get data about the sound. You may use {@link #getAudioFormat()} to get the sound audio format, to be used with the {@link javax.sound.sampled} API.
 *
 * See the project README (on Github) for some context and various examples on how to use the library.
 *
 * @see Sound#Sound(Stream)
 * @see Sound#decodeFullyInto(OutputStream)
 */
public class Sound : Stream {
    private MP3Decoder.MP3SoundData soundData;
    private int index;
    //private AudioFormat audioFormat;
    private Stream _input;

    public Sound(Stream input) {
        _input = input;
        soundData = MP3Decoder.Initiate(input);
        if(soundData == null) {
            throw new IOException("No MPEG data in the specified input stream!");
        }
    }

    public int Read() {
        if(index == -1)
            return -1;
        if(index == soundData.samplesBuffer.Length) {
            if(!MP3Decoder.DecodeFrame(soundData)) {
                index = -1;
                soundData.samplesBuffer = null;
                return -1;
            }
            index = 1;
            return soundData.samplesBuffer[0] & 0xFF;
        }
        return soundData.samplesBuffer[index++] & 0xFF;
    }
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override bool CanRead => true;

    public int Read(byte[] buffer) => Read(buffer, 0, buffer.Length);
    
    public override int Read(byte[] buffer, int offset, int count) {
        if(buffer == null)
            throw new ArgumentNullException();
        else if(offset < 0 || count < 0 || count > buffer.Length - offset)
            throw new ArgumentOutOfRangeException();
        else if(count == 0)
            return 0;

        if(index == -1)
            return -1;

        int len_ = count;
        while(count > 0) {
            if(index == soundData.samplesBuffer.Length) {
                if(!MP3Decoder.DecodeFrame(soundData)) {
                    index = -1;
                    soundData.samplesBuffer = null;
                    return len_ == count ? -1 : len_ - count;
                }
                index = 0;
            }
            int remaining = soundData.samplesBuffer.Length - index;
            if(remaining > 0) {
                if(remaining >= count) {
                    Array.Copy(soundData.samplesBuffer, index, buffer, offset, count);
                    index += count;
                    return len_;
                }
                Array.Copy(soundData.samplesBuffer, index, buffer, offset, remaining);
                offset += remaining;
                count -= remaining;
                index = soundData.samplesBuffer.Length;
            }
        }
        return 0;
    }

    public override void Flush() => _input.Flush();
    public override long Position { get => _input.Position; set => new NotImplementedException(); }

    public int AvailableBytes() => soundData.samplesBuffer == null ? 0 : soundData.samplesBuffer.Length - index;

    public override void SetLength(long value) => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override long Length => _input.Length;

    public override void Close() {
        if(_input != null) {
            _input.Close();
            _input = null;
            soundData.samplesBuffer = null;
        }
        index = -1;
    }

    public int Decode(Stream output) {
        if(index == -1)
            return 0;

        int remaining = soundData.samplesBuffer.Length - index;
        if(remaining > 0)
            output.Write(soundData.samplesBuffer, index, remaining);

        int read = remaining;
        while(MP3Decoder.DecodeFrame(soundData)) {
            output.Write(soundData.samplesBuffer, 0, soundData.samplesBuffer.Length);
            read += soundData.samplesBuffer.Length;
        }
        soundData.samplesBuffer = null;
        index = -1;
        return read;
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public int SampleFrequency => soundData.frequency;

    public bool IsStereo => soundData.stereo == 1;
}
