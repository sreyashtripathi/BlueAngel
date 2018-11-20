﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CashCat
{
    public partial class ConfigurationFile
    {
      public bool webHookEnabled { get; set; }
      public string webHookURI { get; set; }
      public bool catMode { get; set; }
            

        // Get Current Config file // return object
        public ConfigurationFile ConfigurationFileSetup(string path)
        {
            string jsonPath = path + "\\CashCat.json";
            // read JSON directly from a file
            using (StreamReader file = File.OpenText(jsonPath))
            {
                string json = file.ReadToEnd();

                ConfigurationFile CurrentConfig = JsonConvert.DeserializeObject<ConfigurationFile>(json);

                return CurrentConfig;

            }


        }



    }
}
