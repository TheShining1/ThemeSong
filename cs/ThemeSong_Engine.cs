using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

class OBSRequest
{
	public string requestType;
	public string requestId = null;
	public object requestData;
}

class OBSRequestData : System.Collections.Generic.Dictionary<string, object>{}

class OBSRequestBatchRequests : System.Collections.Generic.List<OBSRequest>{};

struct OBSMediaInputSettings
{
	public bool close_when_inactive;
	public bool  hw_decode;
	public string local_file;
}

public class CPHInline
{
	string LogPrefix = "TSO ThemeSong :: ";
	[DllImport("user32.dll")]
	static extern int GetAsyncKeyState(int key);
	//int[] keys = {0x57, 0x41, 0x53, 0x44, 0x20}; // WASD + Space

	static public int obsConnectionID = 0;
	static public string scene = "";
	static public string source = "";
	static public string themeSongPath = "";
	static public int[] keys = new int[]{};
	static public double volumePercent = 1.0;

	static public bool isThemeSongEnded = false;
	static public bool isThemeSongPlaying = false;

	public bool Execute()
	{
		return true;
	}

	public bool IsConfigValid()
	{
		obsConnectionID =  args.ContainsKey("obsConnectionID") ? Convert.ToInt32(args["obsConnectionID"].ToString()) : 0;

		if (!CPH.ObsIsConnected(obsConnectionID))
		{
			CPH.LogDebug($"{LogPrefix}Config is invalid, OBS on connection ID {obsConnectionID} is not connected.");
			return false;
		}

		if (!args.ContainsKey("themeSongSource"))
		{
			CPH.LogDebug($"{LogPrefix}Config is invalid, themeSongSource argument is missing.");
			return false;
		}
		source = args["themeSongSource"].ToString();

		if (!args.ContainsKey("themeSongPath"))
		{
			CPH.LogDebug($"{LogPrefix}Config is invalid, themeSongSource argument is missing.");
			return false;
		}

		themeSongPath = args["themeSongPath"].ToString();
		if (!File.Exists(themeSongPath)) {
			CPH.LogDebug($"{LogPrefix}Config is invalid, themeSongPath file does not exist.");
			return false;
		}

		if (args.ContainsKey("volumePercent"))
		{
			volumePercent = Convert.ToDouble(args["volumePercent"].ToString());
		}

		if (!args.ContainsKey("keys"))
		{
			CPH.LogDebug($"{LogPrefix}Config is invalid, keys argument is missing.");
			return false;
		}
		keys = args["keys"].ToString().Split(',').Select(val => hexStringToIntConverter(val)).ToArray();

		return true;
	}

	public bool GetCurrentScene()
	{
		scene = CPH.ObsGetCurrentScene(obsConnectionID);
		if (scene == "")
		{
			CPH.LogDebug($"{LogPrefix}Config is invalid, scene argument is empty.");
			return false;
		}

		return true;
	}

	public bool CreateThemeSongMediaInput()
	{
		var requests = new OBSRequestBatchRequests();

		var createInputRequest = new OBSRequest{
			requestType = "CreateInput",
			requestData = new OBSRequestData{
				{"sceneName", scene},
				{"inputName", source},
				{"inputKind", "ffmpeg_source"},
				{"inputSettings", new OBSMediaInputSettings{
				  close_when_inactive = true,
				  hw_decode = true,
				  local_file = themeSongPath
				}},
				{"sceneItemEnabled", true}
			}
		};
		requests.Add(createInputRequest);

		var setInputVolumeRequest = new OBSRequest{
			requestType = "SetInputVolume",
			requestData = new OBSRequestData{
				{"inputName", source},
				{"inputVolumeMul", volumePercent}
			}
		};
		requests.Add(setInputVolumeRequest);

		var sleepRequest = new OBSRequest{
			requestType = "Sleep",
			requestData = new OBSRequestData{
				{"sleepMillis", 100}
			}
		};
		requests.Add(sleepRequest);

		var triggerMediaInputActionRequest = new OBSRequest{
			requestType = "TriggerMediaInputAction",
			requestData = new OBSRequestData{
				{"inputName", source},
				{"mediaAction", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE"}
			}
		};
		requests.Add(triggerMediaInputActionRequest);

		string data = JsonConvert.SerializeObject(requests);

		CPH.ObsSendBatchRaw(data, false, 0, obsConnectionID);

		return true;
	}

	public bool RemoveThemeSongMediaInput()
	{
		var removeInputRequestData = new OBSRequestData{
			{"inputName", source}
		};

		CPH.ObsSendRaw("RemoveInput", JsonConvert.SerializeObject(removeInputRequestData), obsConnectionID);

		return true;
	}

	public bool OnSongEnd()
	{
		isThemeSongEnded = true;
		return true;
	}

	public bool Listener()
	{
		isThemeSongEnded = false;

		while (!isThemeSongEnded) {
			bool isDown = isDownCheck(keys);
			bool isPressed = isPressedCheck(keys);

			if(isPressed && !isThemeSongPlaying) {
				CPH.LogDebug($"{LogPrefix}Play");
				CPH.ObsMediaPlay(scene, source, obsConnectionID);
				isThemeSongPlaying = true;
			} else if (!isDown && isThemeSongPlaying) {
				CPH.LogDebug($"{LogPrefix}Pause");
				CPH.ObsMediaPause(scene, source, obsConnectionID);
				isThemeSongPlaying = false;
			};
			Thread.Sleep(100);
		}

		return true;
	}

	bool isPressedCheck(int[] keys) {
		int state = 0;

		foreach (int key in keys) {
			state = state | GetAsyncKeyState(key);
		}
		return Convert.ToBoolean(state >> 1);
	}

	bool isDownCheck(int[] keys) {
		int state = 0;

		foreach (int key in keys) {
			state = state | GetAsyncKeyState(key);
		}

		return Convert.ToBoolean(state << 1);
	}

	int hexStringToIntConverter(string value)
	{
		return int.Parse(value.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);
	}
}
