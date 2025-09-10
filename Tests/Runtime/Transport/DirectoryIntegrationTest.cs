using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace SimpleNet.Transport.Tests
{
	public class DirectoryIntegrationTest : TransportTestBase
	{
		private const string DirectoryUrl = "https://pyserver-z2q0.onrender.com";
		private bool _connected;

		[UnityTest]
		public IEnumerator Register_Server_200OK_TCP()
		{
			StartHost(true);
			yield return new WaitForSeconds(0.3f);

			string serverName = "SN-RegTest-" + Guid.NewGuid().ToString("N").Substring(0, 8);
			yield return RegisterServer(DirectoryUrl, "127.0.0.1", 7777, serverName);
		}

		[UnityTest]
		public IEnumerator Fetch_Contains_Registered_TCP()
		{
			StartHost(true);
			yield return new WaitForSeconds(0.3f);

			string serverName = "SN-FetchTest-" + Guid.NewGuid().ToString("N").Substring(0, 8);
			yield return RegisterServer(DirectoryUrl, "127.0.0.1", 7777, serverName);
			yield return new WaitForSeconds(0.5f);

			ServerEntry[] entries = Array.Empty<ServerEntry>();
			yield return FetchServers(DirectoryUrl, result => entries = result);

			Assert.IsNotNull(entries, "Directory returned null list");
			Assert.IsTrue(entries.Length > 0, "Directory returned empty list");

			bool found = false;
			foreach (var e in entries)
			{
				if (string.Equals(e.name, serverName, StringComparison.Ordinal))
				{
					found = true;
					break;
				}
			}
			Assert.IsTrue(found, "Did not find our registered server in directory");
		}

		[UnityTest]
		public IEnumerator Connect_To_Fetched_TCP()
		{
			StartHost(true);
			yield return new WaitForSeconds(0.3f);

			string serverName = "SN-ConnTest-" + Guid.NewGuid().ToString("N").Substring(0, 8);
			yield return RegisterServer(DirectoryUrl, "127.0.0.1", 7777, serverName);
			yield return new WaitForSeconds(0.5f);

			ServerEntry[] entries = Array.Empty<ServerEntry>();
			yield return FetchServers(DirectoryUrl, result => entries = result);

			ServerEntry target = null;
			foreach (var e in entries)
			{
				if (string.Equals(e.name, serverName, StringComparison.Ordinal))
				{
					target = e;
					break;
				}
			}
			Assert.IsNotNull(target, "Did not find our registered server in directory");

			StartClient(true);
			yield return new WaitForSeconds(0.2f);

			_client.Connect(target.address);
			yield return new WaitForSeconds(0.5f);

			Assert.IsTrue(_connected, "Client failed to connect to host after directory fetch");
		}

		protected override IEnumerator SetUp()
		{
			_connected = false;
			yield return null;
		}

		protected override IEnumerator Teardown()
		{
			yield return null;
		}

		protected override void OnClientConnected(int id)
		{
			_connected = true;
		}

		protected override void OnClientDisconnected(int id)
		{
			_connected = false;
		}

		[Serializable]
		private class ServerEntry
		{
			public string address;
			public int port;
			public string name;
			public double ts;
		}

		[Serializable]
		private class ServerEntryArray
		{
			public ServerEntry[] servers;
		}

		private static IEnumerator RegisterServer(string url, string address, int port, string name)
		{
			string json = $"{{\"address\":\"{address}\",\"port\":{port},\"name\":\"{name}\"}}";
			using (var req = new UnityWebRequest(url + "/register", "POST"))
			{
				byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
				req.uploadHandler = new UploadHandlerRaw(body);
				req.downloadHandler = new DownloadHandlerBuffer();
				req.SetRequestHeader("Content-Type", "application/json");
				req.chunkedTransfer = false;
				yield return req.SendWebRequest();
				Assert.IsTrue(req.result == UnityWebRequest.Result.Success, "Register failed: " + req.responseCode + " " + req.error + " | Body: " + req.downloadHandler.text);
			}
		}

		private static IEnumerator FetchServers(string url, Action<ServerEntry[]> onDone)
		{
			using (var req = UnityWebRequest.Get(url + "/servers"))
			{
				yield return req.SendWebRequest();
				if (req.result != UnityWebRequest.Result.Success)
				{
					onDone?.Invoke(Array.Empty<ServerEntry>());
					yield break;
				}
				var wrapped = "{\"servers\":" + req.downloadHandler.text + "}";
				var data = JsonUtility.FromJson<ServerEntryArray>(wrapped);
				onDone?.Invoke(data?.servers ?? Array.Empty<ServerEntry>());
			}
		}
	}
}
