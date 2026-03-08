using UnityEngine;
using tk;  // for TcpCarHandler and JSONObject

public class EndTrigger : MonoBehaviour
{
    public PathManager pathManager;
    private bool hasTriggered = false;
    private float cooldown = 2f;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"EndTrigger: OnTriggerEnter with {other.name}, tag: {other.tag}");
        if (!hasTriggered && other.transform.root.CompareTag("Car"))
        {
            Debug.Log("EndTrigger: Car detected, sending track_complete");
            hasTriggered = true;

            GameObject car = other.transform.root.gameObject;
            TcpCarHandler tcpHandler = car.GetComponentInChildren<TcpCarHandler>();
            if (tcpHandler != null)
            {
                // Set flag so next reset regenerates track
                tcpHandler.regenerateOnNextReset = true;

                Debug.Log("EndTrigger: Found TcpCarHandler");
                JSONObject json = new JSONObject(JSONObject.Type.OBJECT);
                json.AddField("msg_type", "track_complete");
                if (tcpHandler.GetClient() != null)
                {
                    tcpHandler.GetClient().SendMsg(json);
                }
                else
                {
                    Debug.LogWarning("No client connected – track_complete not sent");
                }
                Debug.Log("Sent track_complete message");
            }
            else
            {
                Debug.LogError("No TcpCarHandler found on car");
            }

            Invoke(nameof(ResetTrigger), cooldown);
        }
    }

    private void ResetTrigger()
    {
        hasTriggered = false;
    }
}