using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class BaseSceneNodeTests
{
    // Mock implementation of BaseSceneNode for testing purposes
    private class MockSceneNode : BaseSceneNode
    {
        private readonly List<ISceneNode> children = new List<ISceneNode>();

        public override void AddChild(ISceneNode child)
        {
            children.Add(child);
        }

        public override void RemoveChild(ISceneNode child)
        {
            children.Remove(child);
        }

        public override IEnumerable<ISceneNode> GetChildren()
        {
            return children;
        }

        public override void Traverse(Action<ISceneNode> action)
        {
            action(this);
            foreach (ISceneNode child in children)
            {
                child.Traverse(action);
            }
        }
    }

    private class MockSceneNodeA : MockSceneNode { }

    private class MockSceneNodeB : MockSceneNode { }

    private MockSceneNode rootNode;
    private MockSceneNode childNode1;
    private MockSceneNode childNode2;
    private MockSceneNodeA nodeA;
    private MockSceneNodeB nodeB;

    [SetUp]
    public void Setup()
    {
        rootNode = new MockSceneNode
        {
            DisplayName = "RootNode",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode1 = new MockSceneNode
        {
            DisplayName = "ChildNode1",
            InstanceID = Guid.NewGuid().ToString(),
        };

        childNode2 = new MockSceneNode
        {
            DisplayName = "ChildNode2",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeA = new MockSceneNodeA
        {
            DisplayName = "NodeA",
            InstanceID = Guid.NewGuid().ToString(),
        };

        nodeB = new MockSceneNodeB
        {
            DisplayName = "NodeB",
            InstanceID = Guid.NewGuid().ToString(),
        };
    }

    [Test]
    public void SetParent_ValidParent_UpdatesParentReferences()
    {
        rootNode.AddChild(childNode1);
        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode, childNode1.GetParent());
        Assert.AreEqual(childNode1, rootNode.GetChildren().First());
    }

    [Test]
    public void GetParentOfType_ValidType_ReturnsCorrectParent()
    {
        childNode1.SetParent(nodeB);
        MockSceneNode result = childNode1.GetParentOfType<MockSceneNodeB>();

        Assert.IsTrue(
            ReferenceEquals(nodeB, result),
            "The parent node returned is not the same instance as expected."
        );
    }

    [Test]
    public void GetParentOfType_CyclicGraphWithDifferentMockTypes_ThrowsInvalidSceneOperationException()
    {
        rootNode.SetParent(childNode1);
        childNode1.SetParent(rootNode);

        Assert.AreEqual(rootNode, childNode1.GetParent(), "NodeB's parent should be NodeA.");
        Assert.AreEqual(childNode1, rootNode.GetParent(), "NodeA's parent should be NodeB.");

        Assert.Throws<InvalidSceneOperationException>(
            () => childNode1.GetParentOfType<MockSceneNodeA>(),
            "Cycle detection did not throw an exception as expected."
        );
    }

    [Test]
    public void SetOwnerInstanceID_AllowedID_SetsSuccessfully()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

        Assert.DoesNotThrow(() => childNode1.SetOwnerInstanceID("Owner1"));
        Assert.AreEqual("Owner1", childNode1.OwnerInstanceID);
    }

    [Test]
    public void SetOwnerInstanceID_DisallowedID_ThrowsGameStateException()
    {
        childNode1.AllowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };

        GameStateException ex = Assert.Throws<GameStateException>(
            () => childNode1.SetOwnerInstanceID("InvalidOwner")
        );
        Assert.That(ex.Message, Does.Contain("Invalid OwnerInstanceID"));
    }

    [Test]
    public void GetChildrenByOwnerInstanceID_ValidID_ReturnsMatchingChildren()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner2";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        IEnumerable<ISceneNode> matchingChildren = rootNode.GetChildrenByOwnerInstanceID("Owner1");
        Assert.AreEqual(1, matchingChildren.Count());
        Assert.AreEqual(childNode1, matchingChildren.First());
    }

    [Test]
    public void Traverse_HierarchicalNodes_VisitsAllNodes()
    {
        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        List<ISceneNode> visitedNodes = new List<ISceneNode>();
        rootNode.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(3, visitedNodes.Count); // rootNode + childNode1 + childNode2
    }

    [Test]
    public void SetParent_ChangesParent_UpdatesLastParent()
    {
        childNode1.SetParent(rootNode);
        childNode1.SetParent(null);

        Assert.AreEqual(rootNode, childNode1.GetLastParent());
        Assert.IsNull(childNode1.GetParent());
    }

    [Test]
    public void GetChildrenByOwnerInstanceIDWithType_ValidID_ReturnsMatchingChildren()
    {
        childNode1.OwnerInstanceID = "Owner1";
        childNode2.OwnerInstanceID = "Owner1";

        rootNode.AddChild(childNode1);
        rootNode.AddChild(childNode2);

        IEnumerable<MockSceneNode> matchingChildren =
            rootNode.GetChildrenByOwnerInstanceID<MockSceneNode>("Owner1");
        Assert.AreEqual(2, matchingChildren.Count());
    }
}
