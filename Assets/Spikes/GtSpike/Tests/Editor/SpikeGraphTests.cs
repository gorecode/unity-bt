using System.IO;
using System.Linq;
using Gorecode.Bt.Spikes.GtSpike;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Gorecode.Bt.Spikes.GtSpike.Tests
{
    // P1 spike: proves GTK authoring + serialization round-trips headlessly.
    // The graphs it creates stay in TestArtifacts (git-ignored): GTK keeps in-memory
    // graph objects for imported graphs, and deleting the files while such an object
    // holds a subgraph node makes it throw on every later domain reload.
    public class SpikeGraphTests
    {
        const string k_ArtifactsDir = "Assets/Spikes/GtSpike/TestArtifacts";
        const string k_SubGraphPath = k_ArtifactsDir + "/SubGraph.gtspike";
        const string k_MainGraphPath = k_ArtifactsDir + "/MainGraph.gtspike";

        [Test]
        public void GraphToolkit_SpikeAuthoringRoundTrips()
        {
            Directory.CreateDirectory(k_ArtifactsDir);

            var sub = GraphDatabase.CreateGraph<SpikeGraph>(k_SubGraphPath);
            Assert.That(sub, Is.Not.Null);
            AuthorSimpleChain(sub);
            GraphDatabase.SaveGraph(sub);

            var graph = GraphDatabase.CreateGraph<SpikeGraph>(k_MainGraphPath);
            Assert.That(graph, Is.Not.Null);

            graph.UndoBeginRecordGraph("Author spike main graph");

            var root = new SpikeRoot();
            var selector = new SpikeSelector();
            var sequence = new SpikeSequence();
            var parallel = new SpikeParallel();
            var cooldown = new SpikeCooldown();
            var moveTo = new SpikeMoveTo();
            var wait = new SpikeWait();
            var log = new SpikeLog();
            var bbWrite = new SpikeBlackboardWrite();
            var bbCondition = new SpikeBlackboardCondition();
            var loop = new SpikeLoop();

            graph.AddNode(root);
            graph.AddNode(selector);
            graph.AddNode(sequence);
            graph.AddNode(parallel);
            graph.AddNode(cooldown);
            graph.AddNode(moveTo);
            graph.AddNode(wait);
            graph.AddNode(log);
            graph.AddNode(bbWrite);
            graph.AddNode(bbCondition);
            graph.AddNode(loop);

            var subNode = graph.AddSubgraphNode(sub, new Vector2(600, 200));
            Assert.That(subNode, Is.Not.Null);

            Assert.That(graph.Connect(root.GetOutputPortByName("Out"), selector.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(selector.GetOutputPortByName("Children"), sequence.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(sequence.GetOutputPortByName("Children"), cooldown.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(cooldown.GetOutputPortByName("Out"), moveTo.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(selector.GetOutputPortByName("Children"), bbCondition.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(bbCondition.GetOutputPortByName("Out"), parallel.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(parallel.GetOutputPortByName("Main"), wait.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(parallel.GetOutputPortByName("Background"), log.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(wait.GetOutputPortByName("Out"), loop.GetInputPortByName("In")), Is.True);
            Assert.That(graph.Connect(loop.GetOutputPortByName("Out"), bbWrite.GetInputPortByName("In")), Is.True);

            graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(graph);

            // 11 spike nodes + 1 subgraph node.
            Assert.That(graph.NodeCount, Is.EqualTo(12));

            var reloaded = GraphDatabase.LoadGraph<SpikeGraph>(k_MainGraphPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.NodeCount, Is.EqualTo(12));

            var reloadedRoot = reloaded.GetNodes().OfType<SpikeRoot>().Single();
            Assert.That(reloadedRoot.GetOutputPortByName("Out").IsConnected, Is.True);

            var reloadedSubNodes = reloaded.GetNodes().OfType<ISubgraphNode>().ToList();
            Assert.That(reloadedSubNodes.Count, Is.EqualTo(1));
        }

        static void AuthorSimpleChain(SpikeGraph graph)
        {
            graph.UndoBeginRecordGraph("Author spike subgraph");
            var root = new SpikeRoot();
            var wait = new SpikeWait();
            graph.AddNode(root);
            graph.AddNode(wait);
            Assert.That(graph.Connect(root.GetOutputPortByName("Out"), wait.GetInputPortByName("In")), Is.True);
            graph.UndoEndRecordGraph();
        }
    }
}
