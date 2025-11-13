using UnityEngine;
using ZFrame;

public class Main : MonoBehaviour
{
    private Frame frame;
    // Start is called before the first frame update
    void Start()
    {
        frame = new Frame();

        DiscoverTools.Discover(typeof(Main).Assembly);

        Frame.DispatchEvent(new StartUpEvent());
    }

    // Update is called once per frame
    void Update()
    {

    }
}
