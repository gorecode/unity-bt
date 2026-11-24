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

        // Grid of the top-down layout.
        const float k_LevelHeight = 200f;
        const float k_ColumnWidth = 300f;

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

            // One depth level per row, each node centered over its children (matches
            // the vertical ports: entry on top, exits at the bottom).
            root.Position = At(0.75f, 0);
            selector.Position = At(0.75f, 1);
            sequence.Position = At(0f, 2);
            cooldown.Position = At(0f, 3);
            moveTo.Position = At(0f, 4);
            bbCondition.Position = At(1.5f, 2);
            parallel.Position = At(1.5f, 3);
            wait.Position = At(1f, 4);
            loop.Position = At(1f, 5);
            bbWrite.Position = At(1f, 6);
            log.Position = At(2f, 4);

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

            var subNode = graph.AddSubgraphNode(sub, At(3f, 0));
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

            // The layout survives the round-trip.
            Assert.That(reloadedRoot.Position, Is.EqualTo(At(0.75f, 0)));
            Assert.That(reloaded.GetNodes().OfType<SpikeSelector>().Single().Position, Is.EqualTo(At(0.75f, 1)));
            Assert.That(reloaded.GetNodes().OfType<SpikeLog>().Single().Position, Is.EqualTo(At(2f, 4)));
            Assert.That(reloaded.GetNodes().OfType<SpikeBlackboardWrite>().Single().Position, Is.EqualTo(At(1f, 6)));

            var reloadedSubNodes = reloaded.GetNodes().OfType<ISubgraphNode>().ToList();
            Assert.That(reloadedSubNodes.Count, Is.EqualTo(1));
        }

        static void AuthorSimpleChain(SpikeGraph graph)
        {
            graph.UndoBeginRecordGraph("Author spike subgraph");
            var root = new SpikeRoot();
            var wait = new SpikeWait();
            root.Position = At(0f, 0);
            wait.Position = At(0f, 1);
            graph.AddNode(root);
            graph.AddNode(wait);
            Assert.That(graph.Connect(root.GetOutputPortByName("Out"), wait.GetInputPortByName("In")), Is.True);
            graph.UndoEndRecordGraph();
        }

        /// <summary>A grid cell: <paramref name="columns"/> column widths from the left edge, <paramref name="level"/> rows down.</summary>
        static Vector2 At(float columns, int level)
        {
            return new Vector2(columns * k_ColumnWidth, level * k_LevelHeight);
        }
    }
}
