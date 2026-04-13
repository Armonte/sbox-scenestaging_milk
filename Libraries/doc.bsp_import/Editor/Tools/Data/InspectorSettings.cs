using System;
using System.Collections.Generic;
using System.Text;

namespace BspImport.Tools.Data;

internal class InspectorSettings
{
	[WideMode]
	[Property, FilePath( Extension = "bsp" )]
	public string FilePath { get; set; } = string.Empty;

	[Property, EnumButtonGroup]
	public InspectorSection Section { get; set; }
}

public enum InspectorSection
{
	Entities,
	TexDataStringData
}
