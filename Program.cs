using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace mlconverter
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1)
            {
                string filePath = arguments[1];

                Console.WriteLine("Path: " + arguments[1].ToString());

                convert(filePath);

                Console.WriteLine("Conversion complete...");
                
                Console.ReadKey();

                Console.Clear();
            }
        }

        static void convert(string filePath)
        {
            BinaryReader gba = new BinaryReader(File.Open(filePath, FileMode.Open));
            BinaryWriter mid = new BinaryWriter(File.Create(AppDomain.CurrentDomain.BaseDirectory + Path.GetFileNameWithoutExtension(filePath) + ".mid"));

            // adjust bufferheight
            Console.BufferHeight = ((gba.BaseStream.Length / 2) < Int16.MaxValue -1) ? ((int)gba.BaseStream.Length / 2) : Int16.MaxValue - 1;

            // read header and shit
            int channelCount = countFlag(gba.ReadUInt16());

            ushort[] channelPointers = new ushort[channelCount];
            for (int i = 0; i < channelCount; i++) channelPointers[i] = gba.ReadUInt16();

            // write midi header
            mid.Write(0x6468544D);
            mid.Write(0x06000000);
            mid.Write(Convert.ToUInt16(0x0100));
            mid.Write(Endian.SwapEndianU16(Convert.ToUInt16(channelCount + 1)));
            mid.Write(Convert.ToUInt16(0xC003));

            // write first midi channel
            mid.Write(0x6B72544D);
            mid.Write(0x0B000000);
            mid.Write(Convert.ToByte(0));
            mid.Write(Convert.ToByte(0xFF)); mid.Write(Convert.ToByte(0x51)); mid.Write(Convert.ToByte(0x03));  // FF meta event
            mid.Write(Convert.ToByte(0x05)); mid.Write(Convert.ToByte(0x07)); mid.Write(Convert.ToByte(0xC6));  // the parameters
            mid.Write(0x002FFF00);
            
            // read and write each channel
            for (int i = 0; i < channelCount; i++)
            {
                // stuff to begin each channel with
                mid.Write(0x6B72544D);
                int lengthPos = (int)mid.BaseStream.Position;   // this variable is used to jump back later to write the length of the track
                mid.Write(0xFFFFFFFF);
                gba.BaseStream.Position = channelPointers[i];

                uint rest = 0;
                uint length = 0;
                int volume = 0;

                while (true)
                {
                    byte status = gba.ReadByte();
                    byte par = gba.ReadByte();

                    if (status == 0xF9)         // tempo
                    {
                        Console.WriteLine("Tempo change: 0x" + par.ToString("X"));
                    }
                    else if (status == 0xF2)    // panning
                    {
                        mid.Write(Convert.ToByte(0));
                        mid.Write(Convert.ToUInt16(0x0AB0 + i));
                        mid.Write(Convert.ToByte(par / 2));

                        Console.WriteLine("Panning change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                    }
                    else if (status == 0xF0)    // instrument
                    {
                        mid.Write(Convert.ToByte(0));
                        mid.Write(Convert.ToByte(0xC0 + i));
                        mid.Write(par);

                        Console.WriteLine("Patch change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                    }
                    else if (status == 0xF1)    // volume
                    {
                        volume = par / 2;

                        Console.WriteLine("Volume change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                    }
                    else if (status == 0xFF)    // end of track
                    {
                        mid.Write(0x002FFF00);

                        Console.WriteLine("End of track " + (i + 1).ToString());

                        break;
                    }
                    else if (status == 0xF8)    // loop
                    {
                        gba.BaseStream.Position++;
                    }
                    else if (status == 0xF6)
                    {
                        rest += par;
                    }
                    else if (status == 0) // special note command, the last byte is the length!!
                    {
                        length += gba.ReadByte();
                    }
                    else if (status == 0xF6)
                    {
                        rest += par;
                    }
                    else
                    {
                        length += status;

                        // write rest
                        toVLV(rest * 0x14, mid);

                        // note on
                        mid.Write(Convert.ToByte(0x90 + i));
                        mid.Write(par);
                        mid.Write(Convert.ToByte(volume));

                        // delta time
                        toVLV(length * 0x14, mid);

                        // note off
                        mid.Write(Convert.ToByte(0x80 + i));
                        mid.Write(par);
                        mid.Write(Convert.ToByte(0));

                        Console.WriteLine("Write note: " + par.ToString() + " volume: " + volume.ToString());

                        rest = 0;
                        length = 0;
                    }
                }

                int originalPos = (int)mid.BaseStream.Position;
                int trackLength = (int)mid.BaseStream.Position - lengthPos;
                mid.BaseStream.Position = lengthPos;
                mid.Write(Endian.SwapEndian32(trackLength - 4));
                mid.BaseStream.Position = originalPos;
            }

            
            // close the streams
            gba.Close();
            mid.Close();
        }

        static private int countFlag(ushort flag)
        {
            int count = 0;

            for (int i = 0; i < 16; i++)
            {
                if ((flag & (1 << i)) != 0) count++;
            }

            return count;
        }

        static void toVLV(uint value, BinaryWriter file)
        {
            uint buffer;
            buffer = value & 0x7F;

            while (Convert.ToBoolean(value >>= 7))
            {
                buffer <<= 8;
                buffer |= ((value & 0x7F) | 0x80);
            }

            while (true)
            {
                file.Write(Convert.ToByte((buffer << 24) >> 24));
                if (Convert.ToBoolean(buffer & 0x80)) buffer >>= 8;
                else break;
            }
        }
    }
}
