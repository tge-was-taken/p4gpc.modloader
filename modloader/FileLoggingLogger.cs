using Reloaded.Mod.Interfaces;
using System;
using System.Drawing;
using System.IO;

namespace modloader
{
    public class FileLoggingLogger : ILogger
    {
        private readonly ILogger mLogger;
        private StreamWriter mWriter;

        public FileLoggingLogger( ILogger logger, string filePath )
        {
            mLogger = logger;
            mWriter = File.CreateText( filePath );
        }

        public Color BackgroundColor { get => mLogger.BackgroundColor; set => mLogger.BackgroundColor = value; }
        public Color TextColor { get => mLogger.TextColor; set => mLogger.TextColor = value; }
        public Color ColorRed { get => mLogger.ColorRed; set => mLogger.ColorRed = value; }
        public Color ColorRedLight { get => mLogger.ColorRedLight; set => mLogger.ColorRedLight = value; }
        public Color ColorGreen { get => mLogger.ColorGreen; set => mLogger.ColorGreen = value; }
        public Color ColorGreenLight { get => mLogger.ColorGreenLight; set => mLogger.ColorGreenLight = value; }
        public Color ColorYellow { get => mLogger.ColorYellow; set => mLogger.ColorYellow = value; }
        public Color ColorYellowLight { get => mLogger.ColorYellowLight; set => mLogger.ColorYellowLight = value; }
        public Color ColorBlue { get => mLogger.ColorBlue; set => mLogger.ColorBlue = value; }
        public Color ColorBlueLight { get => mLogger.ColorBlueLight; set => mLogger.ColorBlueLight = value; }
        public Color ColorPink { get => mLogger.ColorPink; set => mLogger.ColorPink = value; }
        public Color ColorPinkLight { get => mLogger.ColorPinkLight; set => mLogger.ColorPinkLight = value; }
        public Color ColorLightBlue { get => mLogger.ColorLightBlue; set => mLogger.ColorLightBlue = value; }
        public Color ColorLightBlueLight { get => mLogger.ColorLightBlueLight; set => mLogger.ColorLightBlueLight = value; }

        public event EventHandler<string> OnPrintMessage
        {
            add
            {
                mLogger.OnPrintMessage += value;
            }

            remove
            {
                mLogger.OnPrintMessage -= value;
            }
        }

        public void PrintMessage( string message, Color color )
            => mLogger.PrintMessage( message, color );

        public void Write( string message )
            => mLogger.Write( message );

        public void WriteLine( string message )
            => mLogger.WriteLine( message );

        public void Write( string message, Color color )
        {
            mWriter.Write( message );
            mWriter.Flush();
            mLogger.Write( message, color );
        }

        public void WriteLine( string message, Color color )
        {
            mWriter.WriteLine( message );
            mWriter.Flush();
            mLogger.WriteLine( message, color );
        }
    }
}
