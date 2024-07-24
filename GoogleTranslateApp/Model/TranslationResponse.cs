using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleTranslateApp.Model
{
    public class TranslationResponse
    {
        public Data Data { get; set; }
    }

    public class Data
    {
        public Translation[] Translations { get; set; }
    }

    public class Translation
    {
        public string TranslatedText { get; set; }
    }
}
