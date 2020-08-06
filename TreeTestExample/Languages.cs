using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StructuredLogViewerWASM
{
    public class Languages
    {
        public static string LanguagePicker(string fileName)
        {
            string[] fileParts = fileName.Split(".");
            string fileExtension = fileParts[fileParts.Length - 1];
            string language = null;
            switch (fileExtension)
            {
                
            }
            return "";
        }
    }
    public enum LanguagesEnum
    {
        TypeScript,
        JavaScript,
        CSS,
        LESS,
        SCSS,
        JSON,
        HTML,
        XML,
        PHP,
        csharp,
        cpp,
        Razor,
        Markdown,
        Diff,
        Java,
        VB,
        CoffeeScript,
        Handlebars,
        Batch,
        Pug,
        fsharp,
        Lua,
        Powershell,
        Python,
        Ruby,
        SASS,
        R,
    }
}
