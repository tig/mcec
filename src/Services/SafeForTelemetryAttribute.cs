// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace MCEControl;

/// <summary>
/// Used by TELEMETRY to determine which settings are safe for collection.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property)]
public class SafeForTelemetryAttribute : System.Attribute {
}
