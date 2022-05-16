using System;
using System.IO;
using System.Collections.Generic;
using DicomTemplateMakerCSharp.Services;

namespace DicomTemplateMakerCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            DicomTemplateRunner runner = new DicomTemplateRunner();
            runner.run();
        }
    }
}
