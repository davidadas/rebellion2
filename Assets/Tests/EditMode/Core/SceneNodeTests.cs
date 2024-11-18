using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class SceneNodeTestSuite
{
    private class TestSceneNode : SceneNode
    {
        private List<SceneNode> children = new List<SceneNode>();

        public override void AddChild(SceneNode child)
        {
            children.Add(child);
        }

        public override void RemoveChild(SceneNode child)
        {
            children.Remove(child);
        }

        public override IEnumerable<SceneNode> GetChildren()
        {
            return children;
        }
    }

    private class SpecializedTestNode : TestSceneNode { }

    [Test]
    public void SetParent_UpdatesParentAndTypeID()
    {
        TestSceneNode parent = new TestSceneNode { TypeID = "ParentType" };
        TestSceneNode child = new TestSceneNode();

        child.SetParent(parent);

        Assert.AreEqual(parent, child.GetParent());
        Assert.AreEqual("ParentType", child.ParentTypeID);
    }

    [Test]
    public void SetParent_UpdatesLastParent()
    {
        TestSceneNode parent1 = new TestSceneNode();
        TestSceneNode parent2 = new TestSceneNode();
        TestSceneNode child = new TestSceneNode();

        child.SetParent(parent1);
        child.SetParent(parent2);

        Assert.AreEqual(parent2, child.GetParent());
        Assert.AreEqual(parent1, child.GetLastParent());
    }

    [Test]
    public void GetParentOfType_ReturnsClosestMatchingParent()
    {
        SpecializedTestNode root = new SpecializedTestNode();
        TestSceneNode middle = new TestSceneNode();
        TestSceneNode leaf = new TestSceneNode();

        root.AddChild(middle);
        middle.AddChild(leaf);

        middle.SetParent(root);
        leaf.SetParent(middle);

        Assert.AreEqual(root, leaf.GetParentOfType<SpecializedTestNode>());
    }

    [Test]
    public void GetParentOfType_ReturnsNullWhenNoMatch()
    {
        TestSceneNode root = new TestSceneNode();
        TestSceneNode leaf = new TestSceneNode();

        root.AddChild(leaf);
        leaf.SetParent(root);

        Assert.IsNull(leaf.GetParentOfType<SpecializedTestNode>());
    }

    [Test]
    public void GetParentOfType_ReturnsSelfIfMatching()
    {
        SpecializedTestNode node = new SpecializedTestNode();

        Assert.AreEqual(node, node.GetParentOfType<SpecializedTestNode>());
    }

    [Test]
    public void GetChildrenByOwnerTypeID_ReturnsMatchingChildren()
    {
        TestSceneNode root = new TestSceneNode();
        TestSceneNode child1 = new TestSceneNode { OwnerTypeID = "Type1" };
        TestSceneNode child2 = new TestSceneNode { OwnerTypeID = "Type2" };
        TestSceneNode grandchild = new TestSceneNode { OwnerTypeID = "Type1" };

        root.AddChild(child1);
        root.AddChild(child2);
        child1.AddChild(grandchild);

        List<TestSceneNode> result = root.GetChildrenByOwnerTypeID<TestSceneNode>("Type1").ToList();

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains(child1));
        Assert.IsTrue(result.Contains(grandchild));
    }

    [Test]
    public void GetChildrenByOwnerTypeID_ReturnsEmptyWhenNoMatch()
    {
        TestSceneNode root = new TestSceneNode();
        TestSceneNode child = new TestSceneNode { OwnerTypeID = "Type1" };

        root.AddChild(child);

        IEnumerable<SpecializedTestNode> result =
            root.GetChildrenByOwnerTypeID<SpecializedTestNode>("Type1");

        Assert.IsEmpty(result);
    }

    [Test]
    public void Traverse_VisitsAllNodes()
    {
        TestSceneNode root = new TestSceneNode();
        TestSceneNode child1 = new TestSceneNode();
        TestSceneNode child2 = new TestSceneNode();
        TestSceneNode grandchild = new TestSceneNode();

        root.AddChild(child1);
        root.AddChild(child2);
        child1.AddChild(grandchild);

        List<SceneNode> visitedNodes = new List<SceneNode>();
        root.Traverse(node => visitedNodes.Add(node));

        Assert.AreEqual(4, visitedNodes.Count);
        Assert.IsTrue(visitedNodes.Contains(root));
        Assert.IsTrue(visitedNodes.Contains(child1));
        Assert.IsTrue(visitedNodes.Contains(child2));
        Assert.IsTrue(visitedNodes.Contains(grandchild));
    }

    [Test]
    public void AddChild_AddsChildToCollection()
    {
        TestSceneNode parent = new TestSceneNode();
        TestSceneNode child = new TestSceneNode();

        parent.AddChild(child);

        Assert.IsTrue(parent.GetChildren().Contains(child));
    }

    [Test]
    public void RemoveChild_RemovesChildFromCollection()
    {
        TestSceneNode parent = new TestSceneNode();
        TestSceneNode child = new TestSceneNode();

        parent.AddChild(child);
        parent.RemoveChild(child);

        Assert.IsFalse(parent.GetChildren().Contains(child));
    }

    [Test]
    public void GetChildren_ReturnsAllAddedChildren()
    {
        TestSceneNode parent = new TestSceneNode();
        TestSceneNode child1 = new TestSceneNode();
        TestSceneNode child2 = new TestSceneNode();

        parent.AddChild(child1);
        parent.AddChild(child2);

        List<SceneNode> children = parent.GetChildren().ToList();

        Assert.AreEqual(2, children.Count);
        Assert.IsTrue(children.Contains(child1));
        Assert.IsTrue(children.Contains(child2));
    }
}
