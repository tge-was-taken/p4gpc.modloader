using Reloaded.Mod.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;

namespace modloader
{
    public class SemanticLogger
    {
        private readonly ILogger mLogger;

        public SemanticLogger( ILogger logger, string category )
        {
            mLogger = logger;
            Category = category;
        }

        public string Category { get; }

        public void Info( string msg ) => mLogger?.WriteLine( $"{Category} I {msg}" );
        public void Warning( string msg ) => mLogger?.WriteLine( $"{Category} W {msg}", mLogger.ColorYellow );
        public void Error( string msg ) => mLogger?.WriteLine( $"{Category} E {msg}", mLogger.ColorRed );

        [Conditional( "DEBUG" )]
        public void Debug( string msg ) => mLogger?.WriteLine( $"{Category} D {msg}", mLogger.ColorGreen );
    }
}
