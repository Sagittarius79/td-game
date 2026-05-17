using System.Reflection;
using Unity.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

/// <summary>
/// WebGL-kompatibilis UnityTransport subclass — hosztnév-csatlakozást támogat.
///
/// A stock UnityTransport (NGO 2.3.x / com.unity.transport 2.7.x) a
/// ConnectionData.Address mezőben csak numerikus IP-t fogad el; hosztnévre
/// "Invalid network endpoint" hibával dob és a Netcode `network transport
/// start failure`-rel leáll (innen az utána jövő wasm memory crash).
///
/// Az alatta lévő `NetworkDriver.Connect(FixedString512Bytes hostname, ushort port)`
/// overload viszont elvégzi a baselib HostnameLookup-ot — a böngészőben pedig
/// a WebSocketNetworkInterface a wss://hostname:port URL-t használja, így a
/// TLS cert SNI is megfelelő lesz.
///
/// Használat NetworkGameManager.StartClient-ben (WebGL ágban):
///   1. transport.ConnectionData.Address-be írj egy dummy érvényes IP-t
///      ("127.0.0.1"), hogy a Family-check átmenjen.
///   2. WebGLHostnameTransport.PendingHostname = hostIP;  (a valódi hosztnév)
///   3. NetworkManager.Singleton.StartClient();
///
/// A `Connect(NetworkEndpoint)` override eldobja a dummy endpointot és
/// hosztnévvel hív Connect-et az `m_Driver`-re (reflectionnel olvasva).
/// </summary>
public class WebGLHostnameTransport : UnityTransport
{
    /// <summary>
    /// WebGL csatlakozáshoz használt hosztnév. StartClient előtt kell beállítani.
    /// </summary>
    public static string PendingHostname = null;

    /// <summary>WebGL csatlakozási port (a ConnectionData.Port-ot duplázzuk).</summary>
    public static ushort PendingPort = 0;

    // Reflection cache az UnityTransport private m_Driver mezőjéhez.
    private static FieldInfo s_DriverField;

    protected override NetworkConnection Connect(NetworkEndpoint serverEndpoint)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(PendingHostname))
        {
            return ConnectByHostname(PendingHostname, PendingPort > 0 ? PendingPort : serverEndpoint.Port);
        }
#endif
        return base.Connect(serverEndpoint);
    }

    private NetworkConnection ConnectByHostname(string hostname, ushort port)
    {
        if (s_DriverField == null)
        {
            s_DriverField = typeof(UnityTransport).GetField(
                "m_Driver",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (s_DriverField == null)
        {
            Debug.LogError("[WebGLHostnameTransport] Nem találom a UnityTransport.m_Driver privát mezőt – Unity Transport verzióváltás történt?");
            return default;
        }

        var driver = (NetworkDriver)s_DriverField.GetValue(this);
        Debug.Log($"[WebGLHostnameTransport] Hosztnév-csatlakozás: wss://{hostname}:{port}");
        return driver.Connect(new FixedString512Bytes(hostname), port);
    }
}
