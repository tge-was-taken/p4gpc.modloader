namespace modloader
{
    public class FileInfo
    {
        /// <summary>
        /// Contains the absolute file path to the file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Current read pointer for the file.
        /// </summary>
        public long FilePointer { get; set; }

        public FileInfo(string filePath, long filePointer)
        {
            FilePath = filePath;
            FilePointer = filePointer;
        }
    }
}
