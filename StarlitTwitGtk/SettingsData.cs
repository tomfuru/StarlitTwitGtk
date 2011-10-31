using System;
using System.Xml.Serialization;

namespace StarlitTwitGtk
{
	public class SettingsData : SaveDataClassBase<SettingsData>
	{
		public UserAuthInfo[] AuthInfo = new UserAuthInfo[0];
	}
}

