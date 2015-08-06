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
            convertMlsM64("C:\\Users\\Jelle\\Desktop\\MLMTEST\\rom.gba", 0x19FF38);
            /*
            int fileOffset = 0;
            byte convertWay = 0;
            
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1)
            {
                if (arguments[1].StartsWith("-help") || arguments[1].StartsWith("help"))
                {
                    Console.WriteLine(" Help:");
                    Console.WriteLine("");
                    Console.WriteLine(" >mlconverter [file or rom] [commands]");
                    Console.WriteLine("");
                    Console.WriteLine(" The source file can be the file format itself or a rom");
                    Console.WriteLine(" (note: you will need commands if you are trying to convert from a rom)");
                    Console.WriteLine("");
                    Console.WriteLine(" Commands:");
                    Console.WriteLine("");
                    Console.WriteLine(" -mls2mid              convert a mario luigi SS music format to midi (default)");
                    Console.WriteLine(" -mls2m64              convert a mario luigi SS music format to m64");
                    Console.WriteLine(" -mid2mls              convert a midi to a mario luigi SS music format");
                    Console.WriteLine("");
                    Console.WriteLine(" -a=[address]          read from an address or type \"all\" to convert \r\n                       every music from a (U) rom");
                    Console.WriteLine("");
                    Console.WriteLine(" Example: mlconverter rom.gba -a=0x1B1F24");

                    Console.ReadKey();
                }
                else
                {
                    string filePath = arguments[1];
                    Console.WriteLine("Path: " + arguments[1].ToString());

                    // prepare settings
                    if (arguments.Length > 2)
                    {
                        for (int i = 2; i < arguments.Length; i++)
                        {
                            if (arguments[i].StartsWith("-a"))
                            {
                                string[] content = arguments[2].Split('=');
                                if (content[1] == "all") fileOffset = 0x7FFFFFFF;
                                else fileOffset = Convert.ToInt32(content[1], 16);
                            }
                            else if (arguments[i].StartsWith("-mls2mid")) convertWay = 0;
                            else if (arguments[i].StartsWith("-mls2m64")) convertWay = 1;
                            else if (arguments[i].StartsWith("-mid2mls")) convertWay = 2;
                        }
                    }

                    // execute settings etc
                    if (convertWay == 0)
                    {
                        if (fileOffset == 0x7FFFFFFF)
                        {
                            int[] musicAddresses = getAddresses(filePath);
                            for (int i = 1; i < musicAddresses.Length; i++)
                            {
                                int current = (musicAddresses[i] << 8) >> 8;
                                Console.WriteLine("Address: 0x" + current.ToString("X"));
                                convertMlsMid(filePath, current, i.ToString());
                            }
                            Console.Clear();
                        }
                        else convertMlsMid(filePath, fileOffset);
                    }
                    else if (convertWay == 1)
                    {
                        if (fileOffset == 0x7FFFFFFF)
                        {
                            int[] musicAddresses = getAddresses(filePath);
                            for (int i = 1; i < musicAddresses.Length; i++)
                            {
                                int current = (musicAddresses[i] << 8) >> 8;
                                Console.WriteLine("Address: 0x" + current.ToString("X"));
                                convertMlsM64(filePath, current, i.ToString());
                            }
                            Console.Clear();
                        }
                        else convertMlsM64(filePath, fileOffset);
                    }
                    else if (convertWay == 2)
                    {
                        if (fileOffset == 0x7FFFFFFF)
                        {
                            Console.WriteLine("Cannot convert \"all\"");
                        }
                        else convertMidMls(filePath, fileOffset);
                    }

                    Console.WriteLine("Conversion complete...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
            else
            {
                Console.WriteLine("No file or file path or command");
            }
            /**/
            Console.WriteLine("Conversion complete...");
            Console.ReadKey();
            Console.Clear();
        }

        static void convertMlsMid(string filePath, int startOffset, string addName = "")
        {
            BinaryReader gba = new BinaryReader(File.Open(filePath, FileMode.Open));
            BinaryWriter mid = new BinaryWriter(File.Create(AppDomain.CurrentDomain.BaseDirectory + Path.GetFileNameWithoutExtension(filePath) + addName + ".mid"));

            bool noNoteExtend = true;
            int tempo = 0;

            gba.BaseStream.Position = startOffset;

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
                gba.BaseStream.Position = channelPointers[i] + startOffset;

                uint rest = 0;
                uint length = 0;
                int volume = 0;

                while (true)
                {
                    byte status = gba.ReadByte();
                    byte par = gba.ReadByte();

                    if (status == 0xF9)         // tempo
                    {
                        tempo = par;

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
                        if (noNoteExtend)
                        {
                            volume = par / 2;

                            Console.WriteLine("Volume change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                        }
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
                        noNoteExtend = false;
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
                        noNoteExtend = true;
                    }
                }

                int originalPos = (int)mid.BaseStream.Position;
                int trackLength = (int)mid.BaseStream.Position - lengthPos;
                mid.BaseStream.Position = lengthPos;
                mid.Write(Endian.SwapEndian32(trackLength - 4));
                mid.BaseStream.Position = originalPos;
            }

            // go back and write tempo
            mid.BaseStream.Position = 0x1A; // the tempo is written on a fixed place anyway
            int time = 60000000 / tempo;
            mid.Write(Convert.ToSByte(time >> 16));          // 
            mid.Write(Convert.ToSByte((time << 16) >> 24));  // weird system to write out an int into 3 bytes
            mid.Write(Convert.ToSByte((time << 24) >> 24));  //

            // close the streams
            gba.Close();
            mid.Close();
        }

        static void convertMlsM64(string filePath, int startOffset, string addName = "")
        {
            BinaryReader gba = new BinaryReader(File.Open(filePath, FileMode.Open));
            BinaryWriter m64 = new BinaryWriter(File.Create(AppDomain.CurrentDomain.BaseDirectory + Path.GetFileNameWithoutExtension(filePath) + addName + ".m64"));

            bool noNoteExtend = true;
            int tempo = 0;

            gba.BaseStream.Position = startOffset;

            // adjust bufferheight
            //Console.BufferHeight = ((gba.BaseStream.Length / 2) < Int16.MaxValue - 1) ? ((int)gba.BaseStream.Length / 2) : Int16.MaxValue - 1;
            Console.BufferHeight = 2000;

            // read header and shit
            int channelCount = countFlag(gba.ReadUInt16());

            ushort[] channelPointers = new ushort[channelCount];
            for (int i = 0; i < channelCount; i++) channelPointers[i] = gba.ReadUInt16();

            // write m64 header                         !!!!!! Don't forget to go back and rewrite some data
            m64.Write(Convert.ToUInt16(0x21D3));
            m64.Write(Convert.ToByte(0xD7)); m64.Write(Endian.SwapEndianU16(getFlag(channelCount)));

            int trackHeaderPointer = (int)m64.BaseStream.Position;
            for (int i = 0; i < channelCount; i++) { m64.Write(Convert.ToByte(0x90 + i)); m64.Write(Convert.ToUInt16(0xFFFF)); }

            m64.Write(Convert.ToUInt16(0x50DB));                                    // volume
            m64.Write(Convert.ToUInt16(0x78DD));                                    // tempo
            int tempoOffset = (int)m64.BaseStream.Position - 1;
            m64.Write(Convert.ToByte(0xFD)); m64.Write(Convert.ToUInt16(0xC080));
            m64.Write(Convert.ToByte(0xFB)); m64.Write(Convert.ToUInt16(0x0500));
            m64.Write(Convert.ToByte(0xD6)); m64.Write(Endian.SwapEndianU16(getFlag(channelCount)));
            m64.Write(Convert.ToByte(0xFF));

            int[] trackPointerOffsets = new int[channelCount];
            int[] trackHeaderOffsets = new int[channelCount];

            // write track headers
            for (int i = 0; i < channelCount; i++)
            {
                trackHeaderOffsets[i] = (int)m64.BaseStream.Position;
                
                m64.Write(Convert.ToByte(0xC4));                // start

                m64.Write(Convert.ToByte(0x90));
                trackPointerOffsets[i] = (int)m64.BaseStream.Position;
                m64.Write(Convert.ToUInt16(0xFFFF));   // pointer

                m64.Write(Convert.ToUInt16(0x33D4));            // unknown
                m64.Write(Convert.ToUInt16(0x3FDD));            // Panning
                m64.Write(Convert.ToUInt16(0x7FDF));            // volume
                m64.Write(Convert.ToUInt16(0x00C1));            // Instrument
                m64.Write(Convert.ToUInt16(0x00D3));            // Pitch Bend
                m64.Write(Convert.ToUInt16(0x00D8));            // Vibrato range
                m64.Write(Convert.ToByte(0xFD)); m64.Write(Convert.ToUInt16(0xC080));

                m64.Write(Convert.ToByte(0xFF));                // end
            }

            int[] trackOffsets = new int[channelCount];

            // write notes etc
            for (int i = 0; i < channelCount; i++)
            {
                //m64.Write(Convert.ToUInt16(0x00C2));            // neutralize transposition

                gba.BaseStream.Position = channelPointers[i] + startOffset;

                trackOffsets[i] = (int)m64.BaseStream.Position;

                uint length = 0;
                int volume = 0;

                int panning = 0;

                bool same = true, higher = true, lower = true;
                bool first = true;

                while (true)
                {
                    byte status = gba.ReadByte();
                    byte par = gba.ReadByte();

                    if (status == 0xF9)         // tempo
                    {
                        tempo = par;

                        Console.WriteLine("Tempo change: 0x" + par.ToString("X"));
                    }
                    else if (status == 0xF2)    // panning
                    {
                        panning = par / 2;
                        Console.WriteLine("Panning change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                    }
                    else if (status == 0xF0)    // instrument
                    {
                        Console.WriteLine("Patch change: 0x" + par.ToString("X") + " (" + par.ToString() + ") SKIP");
                    }
                    else if (status == 0xF1)    // volume
                    {
                        if (noNoteExtend)
                        {
                            volume = par / 2;

                            Console.WriteLine("Volume change: 0x" + par.ToString("X") + " (" + par.ToString() + ")");
                        }
                    }
                    else if (status == 0xFF)    // end of track
                    {
                        m64.Write(Convert.ToByte(0xFF));
                        
                        Console.WriteLine("End of track " + (i + 1).ToString() + " written");

                        break;
                    }
                    else if (status == 0xF8)    // loop
                    {
                        gba.BaseStream.Position++;
                    }
                    else if (status == 0xF6)
                    {
                        m64.Write(Convert.ToByte(0xC0));
                        m64.Write(Convert.ToByte(par));
                    }
                    else if (status == 0) // special note command, the last byte is the length!!
                    {
                        length += gba.ReadByte();
                        noNoteExtend = false;
                    }
                    else
                    {
                        length += status;

                        par += 20;

                        if (par >= 0x40 && par <= 0x4F)
                        {
                            if (same) m64.Write(Convert.ToUInt16(0x00C2));

                            if (first) m64.Write(Convert.ToByte(par - 0x40));
                            else m64.Write(Convert.ToByte(par));
                            toVLV(length, m64);
                            m64.Write(Convert.ToByte(volume));

                            Console.WriteLine("Write note: " + par.ToString() + " volume: " + volume.ToString());

                            same = false;
                            higher = true;
                            lower = true;
                        }
                        else if (par <= 0x3F && par > 31)
                        {
                            if (lower) m64.Write(Convert.ToUInt16(0xF4C2));

                            if (first) m64.Write(Convert.ToByte((par + 0x0C) - 0x40));
                            else m64.Write(Convert.ToByte(par + 0x0C));
                            toVLV(length, m64);
                            m64.Write(Convert.ToByte(volume));

                            Console.WriteLine("Write note: " + par.ToString() + " volume: " + volume.ToString());

                            same = true;
                            higher = true;
                            lower = false;
                        }
                        else if (par >= 0x80 && par < 0x8C)
                        {
                            if (higher) m64.Write(Convert.ToUInt16(0x0CC2));

                            if (first) m64.Write(Convert.ToByte((par - 0x0C) - 0x40));
                            else m64.Write(Convert.ToByte(par - 0x0C));
                            toVLV(length, m64);
                            m64.Write(Convert.ToByte(volume));

                            Console.WriteLine("Write note: " + par.ToString() + " volume: " + volume.ToString());

                            same = true;
                            higher = true;
                            lower = false;
                        }
                        else Console.WriteLine("Note missed !-----------------------------------------");

                        if (first) m64.Write(Convert.ToByte(0x10));
                        first = false;

                        length = 0;
                        noNoteExtend = true;
                    }
                }
            }

            // m64.Write(Convert.ToByte(0xFF));

            // rewrite the necessary data
            m64.BaseStream.Position = tempoOffset; // tempo
            m64.Write(Convert.ToByte(tempo));

            m64.BaseStream.Position = trackHeaderPointer;
            for (int i = 0; i < channelCount; i++) // channel header stuff
            {
                m64.BaseStream.Position++;
                m64.Write(Endian.SwapEndianU16(Convert.ToUInt16(trackHeaderOffsets[i])));
            }

            for(int i = 0; i < channelCount; i++)
            {
                m64.BaseStream.Position = trackPointerOffsets[i];
                m64.Write(Endian.SwapEndianU16(Convert.ToUInt16(trackOffsets[i])));
            }


            gba.Close();
            m64.Close();
        }

        static void convertMidMls(string filePath, int startOffset, string addName = "")
        {
            BinaryReader mid = new BinaryReader(File.Open(filePath, FileMode.Open));
            BinaryWriter gba = new BinaryWriter(File.Create(AppDomain.CurrentDomain.BaseDirectory + Path.GetFileNameWithoutExtension(filePath) + addName + ".mls"));







            mid.Close();
            gba.Close();
            
            Console.WriteLine("Not yet complete brah");
        }

        static int[] getAddresses(string filePath)
        {
            int[] musicAddresses = new int[51];
            BinaryReader gba = new BinaryReader(File.Open(filePath, FileMode.Open));

            gba.BaseStream.Position = 0x21CB70;

            for (int i = 0; i < 51; i++) musicAddresses[i] = gba.ReadInt32();

            gba.Close();

            return musicAddresses;
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

        static private ushort getFlag(int count, int shift = 0)
        {
            ushort flag = 0;

            for (int i = 0; i < count; i++)
            {
                flag <<= 1;
                flag += 1;
            }

            return flag <<= shift;
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
