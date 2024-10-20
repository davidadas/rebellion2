using NUnit.Framework;
using System;
using System.Collections.Generic;

// Dummy concrete class to test SceneNode.
class TestSceneNode : SceneNode
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

// Another concrete class to test different types of SceneNode.
class SpecialSceneNode : SceneNode
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

[TestFixture]
public class SceneNodeTests
{
    [Test]
    public void TestSetParent()
    {
        // Create parent and child nodes
        SceneNode parent = new TestSceneNode();
        SceneNode child = new TestSceneNode();
        
        // Set parent node
        child.SetParent(parent);

        // Validate that the parent is set correctly
        Assert.AreEqual(parent, child.GetParent(), "Parent should be set correctly.");
        Assert.AreEqual(parent.TypeID, child.ParentTypeID, "ParentTypeID should match the parent's TypeID.");
    }

    [Test]
    public void TestGetParent()
    {
        // Create parent and child nodes
        SceneNode parent = new TestSceneNode();
        SceneNode child = new TestSceneNode();

        // Set parent node
        child.SetParent(parent);

        // Validate that the parent is retrieved correctly
        Assert.AreEqual(parent, child.GetParent(), "GetParent should return the correct parent.");
    }

    [Test]
    public void TestGetClosestParentOfType()
    {
        // Create hierarchy: specialNode -> parentNode -> childNode
        SceneNode specialNode = new SpecialSceneNode();
        SceneNode parentNode = new TestSceneNode();
        SceneNode childNode = new TestSceneNode();

        // Set up the hierarchy
        parentNode.SetParent(specialNode);
        childNode.SetParent(parentNode);

        // Get the closest parent of the specified type (SpecialSceneNode)
        var closestSpecialNode = childNode.GetClosestParentOfType<SpecialSceneNode>();

        // Validate that the closest parent is the specialNode
        Assert.AreEqual(specialNode, closestSpecialNode, "GetClosestParentOfType<SpecialSceneNode> should return specialNode.");

        // Now test finding TestSceneNode as the closest parent of the child node
        var closestTestNode = childNode.GetClosestParentOfType<TestSceneNode>();
        Assert.AreEqual(parentNode, closestTestNode, "GetClosestParentOfType<TestSceneNode> should return parentNode.");
    }

    [Test]
    public void TestAddChild()
    {
        // Create parent and child nodes
        SceneNode parent = new TestSceneNode();
        SceneNode child = new TestSceneNode();

        // Add child to the parent node
        parent.AddChild(child);

        // Validate that the child is added correctly
        CollectionAssert.Contains(parent.GetChildren(), child, "Child should be added to the parent node.");
    }

    [Test]
    public void TestRemoveChild()
    {
        // Create parent and child nodes
        SceneNode parent = new TestSceneNode();
        SceneNode child = new TestSceneNode();

        // Add and then remove child from the parent node
        parent.AddChild(child);
        parent.RemoveChild(child);

        // Validate that the child is removed correctly
        CollectionAssert.DoesNotContain(parent.GetChildren(), child, "Child should be removed from the parent node.");
    }

    [Test]
    public void TestTraverse()
    {
        // Create root, child1, and child2 nodes
        SceneNode root = new TestSceneNode();
        SceneNode child1 = new TestSceneNode();
        SceneNode child2 = new TestSceneNode();

        // Set up the hierarchy
        root.AddChild(child1);
        root.AddChild(child2);

        // List to track visited nodes during traversal
        var visitedNodes = new List<SceneNode>();

        // Traverse the scene graph
        root.Traverse(node => visitedNodes.Add(node));

        // Validate that all nodes are visited
        Assert.Contains(root, visitedNodes, "Traverse should visit the root node.");
        Assert.Contains(child1, visitedNodes, "Traverse should visit child1.");
        Assert.Contains(child2, visitedNodes, "Traverse should visit child2.");
    }
}
