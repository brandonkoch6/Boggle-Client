using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;
using CustomNetworking;
using BB;


namespace BoggleServerTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestScoreKeeping()
        {
            new Test1Class().run(2000);
        }

        public class Test1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private ManualResetEvent mre3;
            private ManualResetEvent mre4;
            private ManualResetEvent mre5;
            private ManualResetEvent mre6;
            private String s1;
            private object p1;
            private String s2;
            private object p2;
            private String s3;
            private object p3;
            private String s4;
            private object p4;
            private String s5;
            private object p5;
            private String s6;
            private object p6;


            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a BoggleServer and two player clients
                BoggleServer server = new BoggleServer("5", ".../.../.../dictionary.txt", "ABCDEFGHIJKLMNOP");

                TcpClient player1 = null;
                TcpClient player2 = null;

                StringSocket player1SS = null;
                StringSocket player2SS = null;

                try
                {           
                    player1 = new TcpClient("localhost", port);
                    player2 = new TcpClient("localhost", port);

                    // Create the internal sockets
                    Socket player1Socket = player1.Client;
                    Socket player2Socket = player2.Client;

                    // Create the players String Sockets
                    player1SS = new StringSocket(player1Socket, new UTF8Encoding());
                    player2SS = new StringSocket(player2Socket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);
                    mre4 = new ManualResetEvent(false);
                    mre5 = new ManualResetEvent(false);
                    mre6 = new ManualResetEvent(false);


                    // Make the receive requests
                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                    player2SS.BeginReceive(CompletedReceive2, player2SS);
                    player1SS.BeginReceive(CompletedReceive3, player1SS);
                    player2SS.BeginReceive(CompletedReceive4, player2SS);
                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                    player2SS.BeginReceive(CompletedReceive6, player2SS);

                    // Send some commands
                    player1SS.BeginSend("play dalton \n", (e, o) => { }, 1);
                    Thread.Sleep(1000);
                    player2SS.BeginSend("play brandon \n", (e, o) => { }, 1);
                    Thread.Sleep(3000);
                    player1SS.BeginSend("word fakeword \n", (e, o) => { }, 1);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Welcome To Our Boggle Server \r", s1);
                    //Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("Welcome To Our Boggle Server \r", s2);
                    //Assert.AreEqual(2, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 brandon\r", s3);
                    //Assert.AreEqual(3, p3);

                    Assert.AreEqual(true, mre4.WaitOne(timeout), "Timed out waiting 4");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 dalton\r", s4);
                    //Assert.AreEqual(4, p4);

                    Assert.AreEqual(true, mre5.WaitOne(timeout), "Timed out waiting 5");
                    Assert.AreEqual("SCORE: -1 0\r", s5);
                    //Assert.AreEqual(5, p5);

                    Assert.AreEqual(true, mre6.WaitOne(timeout), "Timed out waiting 6");
                    Assert.AreEqual("SCORE: 0 -1\r", s6);
                    //Assert.AreEqual(6, p6);
                }
                finally
                {                    
                    player1SS.Close();
                    //player2SS.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive3(String s, Exception o, object payload)
            {
                s3 = s;
                p3 = payload;
                mre3.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive4(String s, Exception o, object payload)
            {
                s4 = s;
                p4 = payload;
                mre4.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive5(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s5 = s;
                    p5 = payload;
                    mre5.Set();
                }
                // Keep listening
                else
                {
                    StringSocket player1SS = (StringSocket)payload;

                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                }
                    
            }

            // This is the callback for the second receive request.
            private void CompletedReceive6(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s6 = s;
                    p6 = payload;
                    mre6.Set();
                }
                // Keep listening
                else
                {
                    StringSocket player2SS = (StringSocket)payload;

                    player2SS.BeginReceive(CompletedReceive6, player2SS);
                }
            }
        }

        [TestMethod]
        public void TestSummary()
        {
            Thread.Sleep(5000);
            new Test2Class().run(2000);
        }

        public class Test2Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private ManualResetEvent mre3;
            private ManualResetEvent mre4;
            private ManualResetEvent mre5;
            private ManualResetEvent mre6;
            private String s1;
            private object p1;
            private String s2;
            private object p2;
            private String s3;
            private object p3;
            private String s4;
            private object p4;
            private String s5;
            private object p5;
            private String s6;
            private object p6;


            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a BoggleServer and two player clients
                //BoggleServer server = new BoggleServer("10", ".../.../.../dictionary.txt", "ABCDEFGHIJKLMNOP");

                TcpClient player1 = null;
                TcpClient player2 = null;

                StringSocket player1SS = null;
                StringSocket player2SS = null;

                try
                {
                    player1 = new TcpClient("localhost", port);
                    player2 = new TcpClient("localhost", port);

                    // Create the internal sockets
                    Socket player1Socket = player1.Client;
                    Socket player2Socket = player2.Client;

                    // Create the players String Sockets
                    player1SS = new StringSocket(player1Socket, new UTF8Encoding());
                    player2SS = new StringSocket(player2Socket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);
                    mre4 = new ManualResetEvent(false);
                    mre5 = new ManualResetEvent(false);
                    mre6 = new ManualResetEvent(false);


                    // Make the receive requests
                    player1SS.BeginReceive(CompletedReceive1, player1SS);
                    player2SS.BeginReceive(CompletedReceive2, player2SS);
                    player1SS.BeginReceive(CompletedReceive3, player1SS);
                    player2SS.BeginReceive(CompletedReceive4, player2SS);
                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                    player2SS.BeginReceive(CompletedReceive6, player2SS);

                    // Send some commands
                    player1SS.BeginSend("play dalton \n", (e, o) => { }, 1);
                    Thread.Sleep(1000);
                    player2SS.BeginSend("play brandon \n", (e, o) => { }, 1);
                    Thread.Sleep(5000);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Welcome To Our Boggle Server \r", s1);
                    //Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("Welcome To Our Boggle Server \r", s2);
                    //Assert.AreEqual(2, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 brandon\r", s3);
                    //Assert.AreEqual(3, p3);

                    Assert.AreEqual(true, mre4.WaitOne(timeout), "Timed out waiting 4");
                    Assert.AreEqual("START ABCDEFGHIJKLMNOP 5 dalton\r", s4);
                    //Assert.AreEqual(4, p4);

                    Assert.AreEqual(true, mre5.WaitOne(timeout), "Timed out waiting 5");
                    Assert.AreEqual("STOP 0 0 0 0 0 \r", s5);
                    //Assert.AreEqual(5, p5);

                    Assert.AreEqual(true, mre6.WaitOne(timeout), "Timed out waiting 6");
                    Assert.AreEqual("STOP 0 0 0 0 0 \r", s6);
                    //Assert.AreEqual(6, p6);
                }
                finally
                {
                    player1SS.Close();
                    //player2SS.Close();
                }
            }

            #region Receive Callbacks
            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive3(String s, Exception o, object payload)
            {
                s3 = s;
                p3 = payload;
                mre3.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive4(String s, Exception o, object payload)
            {
                s4 = s;
                p4 = payload;
                mre4.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive5(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s5 = s;
                    p5 = payload;
                    mre5.Set();
                }
                // Keep listening
                else
                {
                    StringSocket player1SS = (StringSocket)payload;

                    player1SS.BeginReceive(CompletedReceive5, player1SS);
                }

            }

            // This is the callback for the second receive request.
            private void CompletedReceive6(String s, Exception o, object payload)
            {
                // Only record the message if it is not a time message
                if (!s.StartsWith("TIME"))
                {
                    s6 = s;
                    p6 = payload;
                    mre6.Set();
                }
                // Keep listening
                else
                {
                    StringSocket player2SS = (StringSocket)payload;

                    player2SS.BeginReceive(CompletedReceive6, player2SS);
                }
            }
            #endregion
        }
    }
}
