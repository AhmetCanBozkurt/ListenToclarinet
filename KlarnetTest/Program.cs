using System;
using System.IO;
using System.Linq;
using NAudio.Wave;
using Accord.Audio;
using Accord.Math;
using Accord.Audio.Formats;
using Accord.Math.Transforms;
using MathNet.Numerics;

namespace KlarnetTest
{

    class Program
    {
        static void Main()
        {
            int sampleRate = 44100;
            int seconds = 5;
            string outputFilePath = "output.wav";
            string noteFilePath = "nota_defteri.txt";

            while (true)
            {
                // Ses kaydı
                RecordAudio(outputFilePath, sampleRate, seconds);

                // Fourier dönüşümü ve nota tanımlama
                string note = AnalyzeAudio(outputFilePath, sampleRate);

                // Notayı dosyaya yazma
                File.AppendAllText(noteFilePath, note + Environment.NewLine);

                Console.WriteLine($"Tespit edilen nota: {note}");

                // Kısa bir bekleme süresi ekleyin
                System.Threading.Thread.Sleep(1000);
            }
        }

        static void RecordAudio(string filePath, int sampleRate, int seconds)
        {
            using (var waveIn = new WaveInEvent())
            {
                waveIn.WaveFormat = new WaveFormat(sampleRate, 1);
                var writer = new WaveFileWriter(filePath, waveIn.WaveFormat);

                waveIn.DataAvailable += (s, a) =>
                {
                    writer.Write(a.Buffer, 0, a.BytesRecorded);
                    writer.Flush();
                };

                waveIn.StartRecording();
                Console.WriteLine("Kayıt başlıyor...");

                System.Threading.Thread.Sleep(seconds * 1000);

                waveIn.StopRecording();
                writer.Dispose();
                Console.WriteLine("Kayıt tamamlandı.");
            }
        }

        static string AnalyzeAudio(string filePath, int sampleRate)
        {
            using (var reader = new WaveFileReader(filePath))
            {
                int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                int sampleCount = (int)reader.Length / bytesPerSample;
                float[] buffer = new float[sampleCount];
                int read = 0;
                byte[] byteBuffer = new byte[sampleCount * bytesPerSample];
                while (read < sampleCount)
                {
                    int bytesRead = reader.Read(byteBuffer, 0, byteBuffer.Length);
                    for (int i = 0; i < bytesRead; i += bytesPerSample)
                    {
                        buffer[read++] = BitConverter.ToInt16(byteBuffer, i) / 32768f;
                    }
                }

                // FFT ve baskın frekansın belirlenmesi
                double[] fft = FFT(buffer);
                int maxIndex = Array.IndexOf(fft, fft.Max());
                float dominantFrequency = maxIndex * (float)sampleRate / buffer.Length;

                Console.WriteLine($"En baskın frekans: {dominantFrequency} Hz");

                // Frekansı nota ile eşleştirme
                return FrequencyToNote(dominantFrequency);
            }
        }

        static double[] FFT(float[] data)
        {
            int n = data.Length;
            int m = (int)Math.Log(n, 2);

            // Dizinin uzunluğu 2'nin kuvveti mi kontrol et
            if ((1 << m) != n)
            {
                // 2'nin kuvveti değilse, diziyi 2'nin kuvveti uzunluğuna getir
                int newSize = 1 << (m + 1);
                Array.Resize(ref data, newSize);
                n = newSize;
                m = (int)Math.Log(n, 2);
            }

            // Bit-reverse copy
            double[] real = new double[n];
            double[] imag = new double[n];
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, m);
                real[j] = data[i];
                imag[j] = 0;
            }

            // Danielson-Lanczos algorithm
            for (int s = 1; s <= m; s++)
            {
                int m1 = 1 << s;
                int m2 = m1 >> 1;
                double wReal = 1;
                double wImag = 0;
                double theta = Math.PI / m2;
                double wTempReal = Math.Cos(theta);
                double wTempImag = -Math.Sin(theta);

                for (int j = 0; j < m2; j++)
                {
                    for (int k = j; k < n; k += m1)
                    {
                        int k1 = k + m2;
                        double tReal = wReal * real[k1] - wImag * imag[k1];
                        double tImag = wReal * imag[k1] + wImag * real[k1];

                        real[k1] = real[k] - tReal;
                        imag[k1] = imag[k] - tImag;
                        real[k] += tReal;
                        imag[k] += tImag;
                    }
                    double wTemp = wReal;
                    wReal = wReal * wTempReal - wImag * wTempImag;
                    wImag = wTemp * wTempImag + wImag * wTempReal;
                }
            }

            // Calculate magnitude
            double[] magnitudes = new double[n / 2];
            for (int i = 0; i < n / 2; i++)
            {
                magnitudes[i] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            }
            return magnitudes;
        }

        static int BitReverse(int n, int bits)
        {
            int reversedN = n;
            int count = bits - 1;

            n >>= 1;
            while (n > 0)
            {
                reversedN = (reversedN << 1) | (n & 1);
                count--;
                n >>= 1;
            }

            return ((reversedN << count) & ((1 << bits) - 1));
        }

        static string FrequencyToNote(float frequency)
        {
            // A4 = 440 Hz
            double A4 = 440.0;
            double C0 = A4 * Math.Pow(2, -4.75);
            string[] name = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            int h = (int)Math.Round(12 * Math.Log(frequency / C0, 2));  // base 2 logaritma hesaplama
            int octave = h / 12;
            int n = h % 12;

            return name[n] + octave;
        }
    }
}
