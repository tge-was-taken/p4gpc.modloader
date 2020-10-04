using Reloaded.Mod.Interfaces;
using System;
using System.Diagnostics;
using System.Drawing;
using modloader.Configuration;

namespace modloader
{
    public class SemanticLogger
    {
        private readonly ILogger mLogger;
        private readonly Config mConfiguration;

        public SemanticLogger(ILogger logger, string category, Config configuration)
        {
            mLogger = logger;
            Category = category;
            mConfiguration = configuration;
        }

        public string Category { get; }

        public void Info( string msg )
        {
            if (mConfiguration.VerboseMode)
                mLogger?.WriteLineAsync($"{Category} I {msg}");
        }

        public void Warning( string msg ) => mLogger?.WriteLineAsync( $"{Category} W {msg}", mLogger.ColorYellow );
        public void Error( string msg ) => mLogger?.WriteLineAsync( $"{Category} E {msg}", mLogger.ColorRed );

        [Conditional( "DEBUG" )]
        public void Debug( string msg ) => mLogger?.WriteLineAsync( $"{Category} D {msg}", mLogger.ColorGreen );
    }
}
