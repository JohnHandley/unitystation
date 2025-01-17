using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>
///     Tells client to change world object's transform state ((dis)appear/change pos/start floating)
/// </summary>
public class TransformStateMessage : ServerMessage
{
	public static short MessageType = (short) MessageTypes.TransformStateMessage;
	public bool ForceRefresh;
	public TransformState State;
	public uint TransformedObject;

	///To be run on client
	public override IEnumerator Process()
	{
//		Logger.Log("Processed " + ToString());
		if (TransformedObject == NetId.Invalid)
		{
			//Doesn't make any sense
			yield return null;
		}
		else
		{
			yield return WaitFor(TransformedObject);
			if (NetworkObject && (CustomNetworkManager.Instance._isServer || ForceRefresh))
			{
				//update NetworkObject transform state
				var transform = NetworkObject.GetComponent<CustomNetTransform>();
//				Logger.Log($"{transform.ClientState} ->\n{State}");
				transform.UpdateClientState(State);
			}
		}
	}

	public static TransformStateMessage Send(GameObject recipient, GameObject transformedObject, TransformState state, bool forced = true)
	{
		var msg = new TransformStateMessage
		{
			TransformedObject = transformedObject != null ? transformedObject.GetComponent<NetworkIdentity>().netId : NetId.Invalid,
			State = state,
			ForceRefresh = forced
		};
		msg.SendTo(recipient);
		return msg;
	}

	/// <param name="transformedObject">object to hide</param>
	/// <param name="state"></param>
	/// <param name="forced">
	///     Used for client simulation, use false if already updated by prediction
	///     (to avoid updating it twice)
	/// </param>
	public static TransformStateMessage SendToAll(GameObject transformedObject, TransformState state, bool forced = true)
	{
		var msg = new TransformStateMessage
		{
			TransformedObject = transformedObject != null ? transformedObject.GetComponent<NetworkIdentity>().netId : NetId.Invalid,
			State = state,
			ForceRefresh = forced
		};
		msg.SendToAll();
		return msg;
	}

	public override string ToString()
	{
		return
			$"[TransformStateMessage Parameter={TransformedObject} Active={State.Active} WorldPos={State.WorldPosition} localPos={State.Position} " +
			$"Spd={State.Speed} Imp={State.Impulse} Type={MessageType} Forced={ForceRefresh}]";
	}
}