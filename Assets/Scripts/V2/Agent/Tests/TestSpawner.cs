// TestSpawner.cs - attach this to the same GameObject or any other
using UnityEngine;

public class TestSpawner : MonoBehaviour
{
    private void Start()
    {
        var agent = GetComponent<AgentV2>();
        agent.Name = "Test John";
        agent.AddModule(new TestModule());
        agent.AddModule(new TestConsumer());
    }
}