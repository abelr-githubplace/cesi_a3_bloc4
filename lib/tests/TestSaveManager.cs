using System;
using System.Threading;

using Save;

public class TestSaveManager
{
    public TestSaveManager() {

        object s1 = null;
        object s2 = null;

        Thread process1 = new Thread(() => { s1 = SaveManager.Get(); });
        Thread process2 = new Thread(() => { s2 = SaveManager.Get(); });

        process1.Start();
        process2.Start();

        process1.Join();
        process2.Join();

        s1 == s2
    }
}
