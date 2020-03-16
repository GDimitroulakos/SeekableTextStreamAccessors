using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace SeekableStreamReader {

    public class TextSizeRecord {
        private int m_lineSize = -1;
        private int m_columnSize = -1;

        public int M_LineSize {
            get => m_lineSize;
            set => m_lineSize = value;
        }

        public int M_ColumnSize {
            get => m_columnSize;
            set => m_columnSize = value;
        }
    }

    public class TextPosition {
        private int m_line = -1;
        private int m_column = -1;

        public int M_Line {
            get => m_line;
            set => m_line = value;
        }

        public int M_Column {
            get => m_column;
            set => m_column = value;
        }
    }

    public class TextSpanRecord {
        private TextPosition m_startPosition = new TextPosition();
        private TextPosition m_endPosition = new TextPosition();
        TextSizeRecord m_size = new TextSizeRecord();

        public int M_LineStart {
            get => m_startPosition.M_Line;
            set {
                m_startPosition.M_Line = value;
                m_size.M_LineSize = m_endPosition.M_Line - m_startPosition.M_Line + 1;
            }
        }

        public int M_LineEnd {
            get => m_endPosition.M_Line;
            set {
                m_endPosition.M_Line = value;
                m_size.M_LineSize = m_endPosition.M_Line - m_startPosition.M_Line + 1;
            }
        }

        public int M_ColumnStart {
            get => m_startPosition.M_Column;
            set {
                m_startPosition.M_Column = value;
                m_size.M_ColumnSize = m_endPosition.M_Column - m_startPosition.M_Column;
            }
        }

        public int M_ColumnEnd {
            get => m_endPosition.M_Column;
            set {
                m_endPosition.M_Column = value;
                m_size.M_ColumnSize = m_endPosition.M_Column - m_startPosition.M_Column;
            }
        }
        public TextSizeRecord M_Size => m_size;
    }

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
        /// Actrual window Size in characters.
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
        /// The array holds the position in a text document for which each
        /// character starts its encoding. The i-th entry of the array gives
        /// the position in the byte stream for which the i-th character starts
        /// </summary>
        private TextPosition[] m_charTextPos;

        /// <summary>
        /// Holds the data in character form
        /// </summary>
        private char[] m_dataBuffer;

        /// <summary>
        /// Indicates whether this window contains the last part of the stream
        /// </summary>
        private bool m_EOF;

        public bool M_EOF {
            get => m_EOF;
            set => m_EOF = value;
        }

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
        public TextPosition[] M_CharTextPos {
            get => m_charTextPos;
            set => m_charTextPos = value;
        }

        public char[] M_DataBuffer {
            get => m_dataBuffer;
            set => m_dataBuffer = value;
        }

        public bool IsByteIndexInRange(int index) {
            return (index >= m_startBytePosition && index <= m_endBytePosition) ? true : false;
        }

        public bool IsCharIndexInRange(int index) {
            return ((index >= m_startCharacterIndex && index <= m_endCharacterIndex) ? true : false);
        }
    }

    /// <summary>
    /// This class provides buffered random access capabilities to streams
    /// </summary>
    public class BufferedStreamTextReader : IEnumerable<int> {
        /// <summary>
        /// Holds a list of buffer windows already retrieved in sequence by the stream
        /// </summary>
        private List<BufferWindowRecord> m_bufferWindows = new List<BufferWindowRecord>();

        /// <summary>
        /// Current pointed buffer from which we retreive data
        /// </summary>
        private BufferWindowRecord m_currentBuffer;

        /// <summary>
        /// The input stream for which seeking ability will be given
        /// </summary>
        private Stream m_istream;

        /// <summary>
        /// Buffer window size in characters
        /// </summary>
        private int m_bufferSize;

        /// <summary>
        /// Pointer to stream data. Points to the next character to be retrieved
        /// </summary>
        private int m_streamPointer = 0;

        /// <summary>
        /// Holds the input stream encoding
        /// </summary>
        private Encoding m_streamEncoding;

        /// <summary>
        /// Indicates whether the buffered data are valid or decoding the input
        /// stream is required
        /// </summary>
        private Boolean m_bufferDataValidity = false;

        /// <summary>
        /// Provides access to the current stream encoding 
        /// </summary>
        public Encoding M_StreamEncoding {
            get => m_streamEncoding;
            set {
                if (m_streamEncoding != value) {
                    m_streamEncoding = value;
                    ResetBuffers();
                }
            }
        }

        public int M_BufferSize {
            get => m_bufferSize;
            set {
                if (value != m_bufferSize) {
                    m_bufferSize = value;
                    ResetBuffers();
                }

            }
        }

        /// <summary>
        /// Returns the length of stream in bytes
        /// </summary>
        public int M_Length {
            get {
                return (int)m_istream.Length;
            }
        }

        /// <summary>
        /// Checks if the character index exists in the buffer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        bool CharacterIndexInBuffer(int index) {
            if (m_currentBuffer != null) {
                if (index >= m_currentBuffer.M_StartCharacterIndex &&
                    index < m_currentBuffer.M_StartCharacterIndex + m_bufferSize) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the byte index exists in the buffer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        bool ByteIndexInBuffer(int index) {
            if (index >= m_currentBuffer.M_StartCharacterIndex && index < m_currentBuffer.M_StartCharacterIndex + m_bufferSize) {
                return true;
            }
            return false;
        }


        public BufferedStreamTextReader(Stream mIstream, int bufferSize = 4096, Encoding mStreamEncoding = null) {
            m_istream = mIstream;
            m_streamEncoding = mStreamEncoding != null ? mStreamEncoding : GetEncoding();
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

        public string GetLineRemainder() {
            StringBuilder s = new StringBuilder();
            int c;
            if (LookAhead() != 0) {
                c = NextChar();
                while ( c != '\n' && c != -1) {
                    s.Append((char)c);
                    c = NextChar();
                }
            } else {
                return "";
            }

            return s.ToString();
        }

        /// <summary>
        /// Acquires  the next character from the stream without moving the
        /// pointer to the next subsequent position in the stream
        /// </summary>
        /// <returns></returns>
        public int LookAhead() {
            return this[m_streamPointer];
        }

        public int GoBackwards() {
            m_streamPointer = m_streamPointer - 2;
            return m_streamPointer < 0 ? 0 : this[m_streamPointer++];
        }

        /// <summary>
        /// Provides random access to the stream using an index. The index
        /// refer to the index of character in sequence in the stream
        /// </summary>
        /// <param name="index">Character index in the stream</param>
        /// <returns>Returns the character Unicode value</returns>
        public int this[int index] {
            get {
                
                if (!CharacterIndexInBuffer(index) || !m_bufferDataValidity) {
                    WhereToReadNextCharInStream(index);
                }

                if (m_currentBuffer != null) {
                    if (index - m_currentBuffer.M_StartCharacterIndex < m_currentBuffer.M_WindowSize) {
                        return (int)(m_currentBuffer.M_DataBuffer[index - m_currentBuffer.M_StartCharacterIndex]);
                    }
                    else {
                        return -1;
                    }
                } else {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Given a character index in the stream the method identifies the
        /// segment in the stream where the requested character resides by
        /// either looking forward or in reverse. The returned value indicates
        /// whether the requested index is past the end of the eof
        /// </summary>
        /// <param name="index">Index of character to access</param>
        /// <returns>returns null when the requested index is past the EOF and a reference
        /// to the bufferwindow otherwise</returns> 
        protected BufferWindowRecord WhereToReadNextCharInStream(int index) {
            if (m_bufferWindows.Count != 0) {
                if (index > m_bufferWindows.Last().M_EndCharacterIndex) {
                    // If the index is ahead of what is fetched
                    // Fetch text windows chunks from the stream until the chuck containing 
                    // the index is reached or the eof is reached
                    while (m_currentBuffer != null &&
                           !m_bufferWindows.Last().IsCharIndexInRange(index) &&
                           !m_bufferWindows.Last().M_EOF) {
                        ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition + 1,
                           m_bufferWindows.Last().M_EndCharacterIndex + 1);
                    }
                } else if (index < m_bufferWindows.Last().M_StartCharacterIndex) {
                    // If the index is inside in any of the text windows chunks go inverse
                    // to find the window containing the given index
                    foreach (BufferWindowRecord record in Enumerable.Reverse(m_bufferWindows)) {
                        if (record.IsCharIndexInRange(index)) {
                            m_currentBuffer = record;
                            break;
                        }
                    }
                }
            } else {
                ReadDataIntoBuffer(0, 0);
                while (m_currentBuffer != null &&
                       !m_bufferWindows.Last().IsCharIndexInRange(index) &&
                       !m_bufferWindows.Last().M_EOF) {
                    ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition + 1,
                       m_bufferWindows.Last().M_EndCharacterIndex + 1);
                }
            }

            if (m_currentBuffer == null || !m_currentBuffer.IsCharIndexInRange(index)) {
                return null;
            }

            return m_currentBuffer;
        }

        /*protected int WhereToReadNextByteInStream(int index) {
            int EOFreached = 0;
            if (m_bufferWindows.Count != 0) {
                if (index > m_bufferWindows.Last().M_EndBytePosition) {
                    while (!m_bufferWindows.Last().IsByteIndexInRange(index) && !(EOFreached == -1)) {
                        EOFreached = ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition + 1,
                            m_bufferWindows.Last().M_EndCharacterIndex + 1);
                    }
                } else if (index < m_bufferWindows.Last().M_StartBytePosition) {
                    foreach (BufferWindowRecord record in Enumerable.Reverse(m_bufferWindows)) {
                        if (record.IsByteIndexInRange(index)) {
                            m_currentBuffer = record;
                            break;
                        }
                    }
                }
            } else {
                ReadDataIntoBuffer(0, 0);
                while (!m_bufferWindows.Last().IsByteIndexInRange(index) && !(EOFreached == -1)) {
                    EOFreached = ReadDataIntoBuffer(m_bufferWindows.Last().M_EndBytePosition + 1,
                        m_bufferWindows.Last().M_EndCharacterIndex + 1);
                }
            }
            return EOFreached;
        }*/

        /// <summary>
        /// Clears the buffers and resets the stream pointer
        /// </summary>
        protected void ResetBuffers() {
            ResetStreamPointer();
            m_bufferWindows.Clear();
            m_bufferDataValidity = true;
        }

        /// <summary>
        /// This method sets the file pointer to the index-th character in sequence. The
        /// method converts the given character index to the index of the byte from where
        /// the character begins its encoding 
        /// </summary>
        /// <param name="index">index of the character in stream</param>
        /// <returns>returns -1 if index is past the end of file or the indexed
        /// character code otherwise</returns>
        public int SeekChar(int index) {
            // check if the current window holds valid data...
            if (!m_bufferDataValidity || !CharacterIndexInBuffer(index)) {
                //... if not fetch the appropriate window from the stream that
                // includes the requested character
                WhereToReadNextCharInStream(index);
            }
            // if WhereToReadNextCharInStream returns -1 this means that the index-th
            // character is past the end of the stream otherwise 
            if (m_currentBuffer != null) {
                if (!CharacterIndexInBuffer(index)) {
                    return -1;
                } else {
                    m_istream.Seek(m_currentBuffer.M_CharEncodePos[index], SeekOrigin.Begin);
                    m_istream.Flush();
                    m_streamPointer = m_currentBuffer.M_CharEncodePos[index];
                    return m_currentBuffer.M_DataBuffer[index];
                }
            } else {
                return -1;
            }
        }

        /// <summary>
        /// Resets stream pointer. Doesn't cause the invalidation of buffers
        /// </summary>
        public void ResetStreamPointer() {
            SeekChar(0);
        }

        /// <summary>
        /// Reads and maps data into the buffer starting from the indicated
        /// position measured in bytes and in characters. The returned value
        /// indicates whether the file end is reached resulting in no character
        /// read or the buffer window read
        /// </summary>
        protected BufferWindowRecord ReadDataIntoBuffer(int bPosition, int cPosition) {
            /// <summary>
            ///  The unicode character encoding requires not a fixed number of
            ///  bytes for each character
            ///  The array holds the bytes required to encoded each character
            ///  in sequence in the input stream. The i-th entry gives the number
            ///  of bytes to encode the i-th character. 
            /// </summary>
            int[] charEncodeLength;

            /// <summary>
            /// The array holds the position in the input stream for which each
            /// character starts its encoding. The i-th entry of the array gives
            /// the position in the byte stream for which the i-th character starts
            /// </summary>
            int[] charEncodePos;

            /// <summary>
            /// Holds the data in character form. Always points to the
            /// current character buffer of the last decoded window 
            /// </summary>
            char[] dataBuffer;

            TextPosition[] charTextPositions;

            /// <summary>
            /// Character Position in input stream where buffering starts. Always refers
            /// to the current character buffer.
            /// </summary>
            int bufferStart = 0;

            int i_bufsz;
            int bstart; // Holds the index of the first byte of the next character in the stream
            int bindex; // Holds the stream index of the last byte retrieved in the current buffer
            int cindex; // Holds the index of the last character retrieved in the current buffer
            int charactersDecoded; // Number of characters decoded from the last call to GetChars
            int bt = 0; // Retrieved byte code from the stream
            bool islastWindow = false; // indicates whether the last character of the last windows is reached
            byte[] byteBuffer = new byte[1];
            char[] charBuffer = new char[1];

            // 1. Get the decoder for the indicated encoding
            Decoder decoder = m_streamEncoding.GetDecoder();

            // 2. Set the stream at the specified position
            m_istream.Seek(bPosition, SeekOrigin.Begin);
            m_istream.Flush();

            // 3. Allocate space for the buffer 
            dataBuffer = new char[m_bufferSize];
            charEncodePos = new int[m_bufferSize];
            charTextPositions = new TextPosition[m_bufferSize];
            charEncodeLength = new int[m_bufferSize];

            // 4. Read the stream while buffer is full and end of file is not reached
            // Meanwhile decode the bytes to characters into the buffer
            i_bufsz = 0;
            bindex = bstart = bPosition;
            cindex = 0;
            while (cindex < m_bufferSize && (bt = m_istream.ReadByte()) != -1) {
                byteBuffer[0] = (byte)bt; // read the character code as integer and cast it to byte
                charactersDecoded = decoder.GetChars(byteBuffer, 0, 1, charBuffer, 0); // decode
                if (charactersDecoded != 0) {
                    charEncodePos[cindex] = bstart;   // record current character start
                    charTextPositions[cindex] = new TextPosition();
                    if (cindex > 0) {
                        switch (dataBuffer[cindex - 1]) {
                            case '\n':
                            charTextPositions[cindex].M_Line = charTextPositions[cindex - 1].M_Line + 1;
                            charTextPositions[cindex].M_Column = 0;
                            break;
                            default:
                            charTextPositions[cindex].M_Line = charTextPositions[cindex - 1].M_Line;
                            charTextPositions[cindex].M_Column = charTextPositions[cindex - 1].M_Column + 1;
                            break;
                        }
                    } else {
                        if (m_bufferWindows.Count > 0) {
                            switch (dataBuffer[
                                m_bufferWindows.Last().M_DataBuffer[m_bufferWindows.Last().M_WindowSize - 1]]) {
                                case '\n':
                                charTextPositions[cindex].M_Line = m_bufferWindows.Last().M_CharTextPos[m_bufferWindows.Last().M_WindowSize - 1].M_Line + 1;
                                charTextPositions[cindex].M_Column = 0;
                                break;
                                default:
                                charTextPositions[cindex].M_Line = m_bufferWindows.Last().M_CharTextPos[m_bufferWindows.Last().M_WindowSize - 1].M_Line;
                                charTextPositions[cindex].M_Column = m_bufferWindows.Last().M_CharTextPos[m_bufferWindows.Last().M_WindowSize - 1].M_Column + 1;
                                break;
                            }
                        } else {
                            charTextPositions[cindex].M_Line = 1;
                            charTextPositions[cindex].M_Column = 0;
                        }
                    }

                    charEncodeLength[cindex] = bindex - bstart + 1; // record current character length
                    bstart = bindex + 1; // Next character starts at the next byte position

                    dataBuffer[cindex++] = charBuffer[0]; // Transfer the decoded character into the buffer
                }
                bindex++;
            }

            if (bt == -1 || bindex == m_istream.Length) {
                islastWindow = true;
            }

            m_bufferDataValidity = true;

            if (cindex > 0) {

                // 5. Create new buffer record
                BufferWindowRecord rec = new BufferWindowRecord() {
                    M_StartBytePosition = bPosition,
                    M_EndBytePosition = bindex - 1,
                    M_StartCharacterIndex = cPosition,
                    M_EndCharacterIndex = cPosition + cindex - 1,
                    M_WindowSize = cindex,
                    M_CharEncodePos = charEncodePos,
                    M_CharTextPos = charTextPositions,
                    M_CharEncodeLength = charEncodeLength,
                    M_DataBuffer = dataBuffer,
                    M_EOF = islastWindow
                };

                m_bufferWindows.Add(rec);
                //bufferStart = rec.M_StartCharacterIndex;
                m_currentBuffer = rec;
                return m_currentBuffer;
            } else {
                // cindex ==0 nothing was read from the file because EOF is reached
                return null;
            }
        }

        public IEnumerator<int> GetEnumerator() {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public Encoding GetEncoding() {
            // Read the BOM
            var bom = new byte[4];
            m_istream.Read(bom, 0, 4);

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
                return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe)
                return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff)
                return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
                return Encoding.UTF32;
            return Encoding.ASCII;
        }

        public override string ToString() {
            StringBuilder s = new StringBuilder();
            int k = 0;
            foreach (BufferWindowRecord record in m_bufferWindows) {
                s.Append("\n\nBuffer Window " + k + ":\n");
                s.Append("\tStartBytePosition:" + record.M_StartBytePosition + "\n");
                s.Append("\tEndBytePosition:" + record.M_EndBytePosition + "\n");
                s.Append("\tStartCharacterIndex:" + record.M_StartCharacterIndex + "\n");
                s.Append("\tEndCharacterIndex:" + record.M_EndCharacterIndex + "\n");
                s.Append("\tWindow Size:" + record.M_WindowSize + "\n");
                for (int j = 0; j < record.M_WindowSize; j++) {
                    s.Append("(Char:" + record.M_DataBuffer[j] + ",(Line:" + record.M_CharTextPos[j].M_Line +
                             ",Column:" + record.M_CharTextPos[j].M_Column + "),EncodePos:" + record.M_CharEncodePos[j] +
                             ",EncodeLen:" + record.M_CharEncodeLength[j] + "),");

                }
                s.Append("\n");
                for (int j = 0; j < record.M_WindowSize; j++) {
                    s.Append(record.M_DataBuffer[j]);
                }

                k++;
            }

            s.Append("\n\n\n");
            foreach (BufferWindowRecord record in m_bufferWindows) {
                for (int j = 0; j < record.M_WindowSize; j++) {
                    s.Append(record.M_DataBuffer[j]);
                }

                s.Append(" <<*|*>> ");
            }

            return s.ToString();
        }
    }

    class Program {
        static void Main(string[] args) {
            int ccode;
            StreamWriter dbg = new StreamWriter("debug.txt");
            BufferedStreamTextReader bStreamReader = new BufferedStreamTextReader(new FileStream("test.txt",
                FileMode.Open), 128, Encoding.UTF8);
            int i = 0;
            while ((ccode = bStreamReader.NextChar()) != -1) {
                Console.Write(ccode + " ");
                i++;
            }
            Console.WriteLine();

            while ((ccode = bStreamReader.GoBackwards()) != 0) {
                Console.Write(ccode + " ");
            }
            dbg.WriteLine(bStreamReader.ToString());
            dbg.Close();

            bStreamReader.SeekChar(0);
            string s;
            while ((s = bStreamReader.GetLineRemainder()) != "") {
                Console.WriteLine(s);
            }

            /*Console.WriteLine(bStreamReader[2]);
            Console.WriteLine(bStreamReader[200]);
            Console.WriteLine(bStreamReader[2]);*/
        }
    }
}
