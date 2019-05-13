using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeekableStreamReader {

    public class BufferWindowRecord {
        /// <summary>
        /// Index of first window byte in the underlying stream
        /// </summary>
        private int m_startBytePosition;
        /// <summary>
        /// Index of last window byte in the underlying stream
        /// </summary>
        private int m_endBytePosition;
        /// <summary>
        /// Index of first window character in the underlying stream
        /// </summary>
        private int m_startCharacterIndex;
        /// <summary>
        /// Index of last window character in the underlying stream
        /// </summary>
        private int m_endCharacterIndex;
        /// <summary>
        /// Window Size in characters
        /// </summary>
        private int m_windowSize;
        /// <summary>
        ///  The unicode character encoding requires not a fixed number of
        ///  bytes for each character
        ///  The array holds the bytes required to encoded each character
        ///  in sequence in the input stream. The i-th entry gives the number
        ///  of bytes to encode the i-th character. 
        /// </summary>
        private int[] m_charEncodeLength;

        /// <summary>
        /// The array holds the position in the input stream for which each
        /// character starts its encoding. The i-th entry of the array gives
        /// the position in the byte stream for which the i-th character starts
        /// </summary>
        private int[] m_charEncodePos;

        /// <summary>
        /// Holds the data in character form
        /// </summary>
        private char[] m_dataBuffer;


        public int M_StartBytePosition {
            get => m_startBytePosition;
            set => m_startBytePosition = value;
        }

        public int M_EndBytePosition {
            get => m_endBytePosition;
            set => m_endBytePosition = value;
        }

        public int M_StartCharacterIndex {
            get => m_startCharacterIndex;
            set => m_startCharacterIndex = value;
        }

        public int M_EndCharacterIndex {
            get => m_endCharacterIndex;
            set => m_endCharacterIndex = value;
        }

        public int M_WindowSize {
            get => m_windowSize;
            set => m_windowSize = value;
        }

        public int[] M_CharEncodeLength {
            get => m_charEncodeLength;
            set => m_charEncodeLength = value;
        }

        public int[] M_CharEncodePos {
            get => m_charEncodePos;
            set => m_charEncodePos = value;
        }
        public char[] M_DataBuffer {
            get => m_dataBuffer;
            set => m_dataBuffer = value;
        }

        public bool IsByteIndexInRange(int index) {
            return index >= m_startBytePosition && (index <= m_endBytePosition ? true : false);
        }

        public bool IsCharIndexInRange(int index){
            return index >= m_startCharacterIndex && (index <= m_endCharacterIndex ? true : false);
        }
    }

    /// <summary>
    /// This class provides buffered random access capabilities to streams
    /// </summary>
    public class BufferedStreamTextReader {
        /// <summary>
        /// Holds a list of buffer windows already retrived in sequence by the stream
        /// </summary>
        private List<BufferWindowRecord> m_bufferWindows =new List<BufferWindowRecord>();

        /// <summary>
        /// The input stream for which seeking ability will be given
        /// </summary>
        private Stream m_istream;

        /// <summary>
        /// Holds the data in character form
        /// </summary>
        private char[] m_dataBuffer;

        /// <summary>
        /// Buffer window size in characters
        /// </summary>
        private int m_bufferSize;

        /// <summary>
        /// Character Position in input stream where buffering starts
        /// </summary>
        private int m_bufferStart = 0;

        /// <summary>
        /// Pointer to character data in the buffer
        /// </summary>
        private int m_bufferPointer = 0;

        /// <summary>
        /// Pointer to stream data
        /// </summary>
        private int m_streamPointer = 0;

        /// <summary>
        ///  The unicode character encoding requires not a fixed number of
        ///  bytes for each character
        ///  The array holds the bytes required to encoded each character
        ///  in sequence in the input stream. The i-th entry gives the number
        ///  of bytes to encode the i-th character. 
        /// </summary>
        private int[] m_charEncodeLength;

        /// <summary>
        /// The array holds the position in the input stream for which each
        /// character starts its encoding. The i-th entry of the array gives
        /// the position in the byte stream for which the i-th character starts
        /// </summary>
        private int[] m_charEncodePos;

        /// <summary>
        /// Holds the decoded text from the last decoding processing
        /// </summary>
        private StringBuilder m_decodedText;

        /// <summary>
        /// Holds the input stream encoding
        /// </summary>
        private Encoding m_streamEncoding;

        /// <summary>
        /// Indicates whether the underlying stream has been mapped with the
        /// current encoding. If not, a call to MapStream method will precede
        /// a call to a stream accessor method
        /// </summary>
        private Boolean m_mappingAcknowledge = false;

        private Boolean m_bufferReallocationRequired = true;

        /// <summary>
        /// Provides access to the current stream encoding 
        /// </summary>
        public Encoding M_StreamEncoding {
            get => m_streamEncoding;
            set {
                m_streamEncoding = value;
                m_mappingAcknowledge = false;
            }
        }

        public int M_BufferSize {
            get => m_bufferSize;
            set {
                if (value != m_bufferSize) {
                    m_bufferSize = value;
                    m_bufferReallocationRequired = true;
                }
                
            }
        }

        public int M_BufferStart {
            get => m_bufferStart;
            set {
                m_bufferStart = value;
            }
        }

        public BufferedStreamTextReader(Stream mIstream, int bufferSize = 4096, Encoding mStreamEncoding = null) {
            m_istream = mIstream;
            m_streamEncoding = mStreamEncoding != null ? mStreamEncoding : GetEncoding();
            m_mappingAcknowledge = false;
            m_bufferSize = bufferSize;
        }

        /// <summary>
        /// Acquires  the next character from the stream and moves the
        /// pointer to the next subsequent position in the stream
        /// </summary>
        /// <returns></returns>
        public int NextChar() {
            return this[m_streamPointer++];
        }

        /// <summary>
        /// Acquires  the next character from the stream without moving the
        /// pointer to the next subsequent position in the stream
        /// </summary>
        /// <returns></returns>
        public int LookAhead() {
            return this[m_streamPointer];
        }
        
        /// <summary>
        /// Checks if the character index exists in the buffer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        bool CharacterIndexInBuffer(int index) {
            if (index >= m_bufferStart && index < m_bufferStart + m_bufferSize) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Provides random access to the stream using an index. The index
        /// refer to the index of character in sequence in the stream
        /// </summary>
        /// <param name="index">Character index in the stream</param>
        /// <returns></returns>
        public char this[int index] {
            get {
                if (!m_mappingAcknowledge || !CharacterIndexInBuffer(index)) {
                    //ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition+1);
                    WhereToReadNextInStream(index);
                }

                return m_dataBuffer[index-m_bufferStart];
            }
        }

        /// <summary>
        /// Given a character index in the stream the method identifies the
        /// segment in the stream where the requested character resides by
        /// either looking forward or in reverse.
        /// </summary>
        /// <param name="index">Index of character to access</param>
        protected void WhereToReadNextInStream(int index) {
            if (index > m_bufferWindows.Last().M_EndCharacterIndex) {
                while ( !m_bufferWindows.Last().IsCharIndexInRange(index) ) {
                    ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition + 1);
                }
            }
            else if ( index < m_bufferWindows.Last().M_StartCharacterIndex) {
                foreach (BufferWindowRecord record in Enumerable.Reverse(m_bufferWindows)) {
                    if (record.IsCharIndexInRange(index)) {
                        ReadDataIntoBuffer(record.M_StartBytePosition);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This method sets the file pointer to the index-th byte in sequence
        /// </summary>
        /// <param name="index"></param>
        protected void Seek(int index) {
            m_istream.Seek(index, SeekOrigin.Begin);
            m_istream.Flush();
        }

        /// <summary>
        /// Reads and maps data into the buffer starting from the indicated
        /// position measured in bytes
        /// </summary>
        public void ReadDataIntoBuffer(int bPosition) {
            int i_bufsz;
            int bstart;
            int bindex; // Holds the index of the last byte retrieved
            int cindex; // discovered character index in sequence
            int charactersDecoded;
            int bt;
            byte[] byteBuffer = new byte[1];
            char[] charBuffer = new char[1];

            // 1. Get the decoder for the indicated encoding
            Decoder decoder = m_streamEncoding.GetDecoder();

            // 2. Set the stream at the specified position
            Seek(bPosition);

            // 3. Allocate space for the buffer if necessary
            if (m_bufferReallocationRequired) {
                m_dataBuffer = new char[m_bufferSize];
                m_charEncodePos = new int[m_bufferSize];
                m_charEncodeLength = new int[m_bufferSize];
            }

            // 4. Read the stream while buffer is full and end of file is not reached
            // Meanwhile decode the bytes to characters into the buffer
            i_bufsz = 0;
            bindex = bstart = m_bufferStart = bPosition;
            cindex = 0;
            while (cindex < m_bufferSize && (bt=m_istream.ReadByte())!=-1) {
                byteBuffer[0] = (byte) bt; // read the character code as integer and cast it to byte
                charactersDecoded = decoder.GetChars(byteBuffer, 0, 1, charBuffer, 0); // decode
                if (charactersDecoded != 0) {
                    m_charEncodePos[cindex] = bstart;   // record current character start
                    m_charEncodeLength[cindex] = bindex - bstart + 1; // record current character length
                    bstart = bindex + 1; // Next character starts at the next byte position
                    
                    m_dataBuffer[cindex++] = charBuffer[0]; // Transfer the decoded character into the buffer
                }
                bindex++;
            }
            m_mappingAcknowledge = true;

            // 5. Create new buffer record
            BufferWindowRecord rec = new BufferWindowRecord() {
                M_StartBytePosition = bPosition,
                M_EndBytePosition = bindex-1,
                M_StartCharacterIndex = m_bufferWindows.Count!=0 ?m_bufferWindows.Last().M_EndCharacterIndex+1:0,
                M_EndCharacterIndex = m_bufferWindows.Count != 0 ? m_bufferWindows.Last().M_EndCharacterIndex + cindex:cindex-1,
                M_WindowSize = cindex,
                M_CharEncodePos = m_charEncodePos,
                M_CharEncodeLength = m_charEncodeLength,
                M_DataBuffer = m_dataBuffer
            };
            m_bufferWindows.Add(rec);

            m_bufferStart = rec.M_StartCharacterIndex;

            Console.WriteLine(m_dataBuffer);
        }

        public Encoding GetEncoding() {
            // Read the BOM
            var bom = new byte[4];
            m_istream.Read(bom, 0, 4);

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.ASCII;
        }



    }


    // This class wraps a text stream-subclass object which does not support
    // seeking and offers seeking ability taking into account the text encoding
    public class SeekableStreamTextReader {
        /// <summary>
        /// The input stream for which seeking ability will be given
        /// </summary>
        private Stream m_istream;

        /// <summary>
        ///  The unicode character encoding requires not a fixed number of
        ///  bytes for each character
        ///  The array holds the bytes required to encoded each character
        ///  in sequence in the input stream. The i-th entry gives the number
        ///  of bytes to encode the i-th character. 
        /// </summary>
        private int[] m_charEncodeLength;

        /// <summary>
        /// The array holds the position in the input stream for which each
        /// character starts its encoding. The i-th entry of the array gives
        /// the position in the byte stream for which the i-th character starts
        /// </summary>
        private int[] m_charEncodePos;

        /// <summary>
        /// Holds the decoded text from the last decoding processing
        /// </summary>
        private StringBuilder m_decodedText;

        /// <summary>
        /// Holds the input stream encoding
        /// </summary>
        private Encoding m_streamEncoding;

        /// <summary>
        /// Indicates whether the underlying stream has been mapped with the
        /// current encoding. If not, a call to MapStream method will preeced
        /// a call to a stream accessor method
        /// </summary>
        private Boolean m_mappingAknowledge = false;


        /// <summary>
        /// Provides access to the current stream encoding 
        /// </summary>
        public Encoding M_StreamEncoding {
            get => m_streamEncoding;
            set {
                m_streamEncoding = value;
                m_mappingAknowledge = false;
            }
        }

        public SeekableStreamTextReader(Stream mIstream, Encoding mStreamEncoding = null) {
            m_istream = mIstream;
            m_streamEncoding = mStreamEncoding != null ? mStreamEncoding : GetEncoding();
            m_mappingAknowledge = false;
        }

        /// <summary>
        /// Provides random access to the stream using an index. The index
        /// refer to the index of character in sequence in the stream
        /// </summary>
        /// <param name="index">Character index in the stream</param>
        /// <returns></returns>
        public char this[int index] {
            get {
                if (!m_mappingAknowledge) {
                    MapWholeStream();
                }
                Decoder decoder = m_streamEncoding.GetDecoder();
                byte[] byteBuffer = new byte[m_charEncodeLength[index]];
                char[] charBuffer = new char[1];
                Seek(m_charEncodePos[index]);

                m_istream.Read(byteBuffer, 0, m_charEncodeLength[index]);
                decoder.GetChars(byteBuffer, 0, byteBuffer.Length, charBuffer, 0);
                return charBuffer[0];
            }
        }

        /// <summary>
        /// This method sets the file pointer to the index-th byte in sequence
        /// </summary>
        /// <param name="index"></param>
        protected void Seek(int index) {
            m_istream.Seek(index, SeekOrigin.Begin);
            m_istream.Flush();
        }

        /// <summary>
        /// Set the file pointer to the character specified by the index parameter
        /// </summary>
        /// <param name="index">The index of character</param>
        public void SeekChar(int index) {
            if (!m_mappingAknowledge) {
                MapWholeStream();
            }
            m_istream.Seek(m_charEncodePos[index], SeekOrigin.Begin);
            m_istream.Flush();
        }

        public int CharacterEncodingPosition(int index) {
            if (!m_mappingAknowledge) {
                MapWholeStream();
            }
            return m_charEncodePos[index];
        }

        public int CharacterEncodingLength(int index) {
            if (!m_mappingAknowledge) {
                MapWholeStream();
            }
            return m_charEncodeLength[index];
        }

        /// <summary>
        /// Closes the stream
        /// </summary>
        public void CloseStream() {
            m_istream.Close();
        }

        public void MapWholeStream() {
            // Required declarations to use the .NET API
            int mb, bstart;
            int bindex; // Holds the index of the last byte retrieved
            int cindex; // discovered character index in sequence
            int charactersDecoded;
            byte[] byteBuffer = new byte[1];
            char[] charBuffer = new char[1];

            // Initializations
            m_decodedText = new StringBuilder();

            // Get an encoder for the given encoding
            Decoder decoder = m_streamEncoding.GetDecoder();

            // Read the whole stream byte-by-byte and decode it using the
            // the given encoder. You have to reset the stream first
            m_istream.Position = 0;
            m_istream.Flush();
            bstart = bindex = 0;
            cindex = 0;
            m_charEncodeLength = new int[m_istream.Length];
            m_charEncodePos = new int[m_istream.Length];
            while ((mb = m_istream.ReadByte()) != -1) {
                byteBuffer[0] = (byte)mb;
                // Decoder.GetChars holds state between consecutive invocations thus for every byte
                // it is fed it considers previous bytes that haven't yet being decoded 
                charactersDecoded = decoder.GetChars(byteBuffer, 0, 1, charBuffer, 0);
                if (charactersDecoded != 0) {
                    m_charEncodePos[cindex] = bstart;   // record current character start
                    m_charEncodeLength[cindex] = bindex - bstart + 1; // record current character length
                    cindex++;            // Increase characters discovered
                    bstart = bindex + 1; // Next character starts at the next byte position

                    m_decodedText.Append(charBuffer[0]); // Append the last decoded character to the decoded result
                }
                bindex++; // Point to the next byte in sequence
            }

            m_mappingAknowledge = true;
            Console.WriteLine(m_decodedText);
        }


        public Encoding GetEncoding() {
            // Read the BOM
            var bom = new byte[4];
            m_istream.Read(bom, 0, 4);

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.ASCII;
        }
    }

    class Program {
        static void Main(string[] args) {
            int ccode;
            
            SeekableStreamTextReader sStreamReader = new SeekableStreamTextReader(new FileStream("test.txt", 
                FileMode.Open), Encoding.UTF8);
            sStreamReader.CloseStream();
            BufferedStreamTextReader bStreamReader = new BufferedStreamTextReader(new FileStream("test.txt",
                FileMode.Open),128,Encoding.UTF8);
            bStreamReader.ReadDataIntoBuffer(0);
            while ((ccode= bStreamReader.NextChar()) != 0) {
                Console.Write((char)ccode);
            }
            Console.WriteLine(bStreamReader[2]);
            Console.WriteLine(bStreamReader[200]);
            Console.WriteLine(bStreamReader[2]);

        }
    }
}
