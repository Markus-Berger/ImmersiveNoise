using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

//Adapted from: https://stackoverflow.com/questions/66893/tree-data-structure-in-c-sharp

public class TreeNode<T>
{
    private readonly T _value;
    private readonly List<TreeNode<T>> _children = new List<TreeNode<T>>();

    public TreeNode(T value)
    {
        _value = value;
    }

    public TreeNode<T> this[int i]
    {
        get { return _children[i]; }
    }

    public TreeNode<T> Parent { get; private set; }

    public T Value { get { return _value; } }

    public ReadOnlyCollection<TreeNode<T>> Children
    {
        get { return _children.AsReadOnly(); }
    }

    public TreeNode<T> AddChild(T value)
    {
        var node = new TreeNode<T>(value) { Parent = this };
        _children.Add(node);
        return node;
    }

    public void ConnectChild(TreeNode<T> child)
    {
        _children.Add(child);
        child.Parent = this;
    }

    public TreeNode<T>[] AddChildren(params T[] values)
    {
        return values.Select(AddChild).ToArray();
    }

    public bool RemoveChild(TreeNode<T> node)
    {
        return _children.Remove(node);
    }

    public void RemoveSelf()
    {
        if(Parent == null)
        {
            _children.Clear();
        }
        else
        {
            foreach (TreeNode<T> child in _children)
            {
                Parent.ConnectChild(child);
            }
            Parent.RemoveChild(this);
        }
        
    }

    public void Traverse(Action<T> action)
    {
        action(Value);
        foreach (var child in _children)
            child.Traverse(action);
    }

    public void Reverse(Action<T> action)
    {
        foreach (var child in _children)
            child.Reverse(action);
        action(Value);
    }

    public IEnumerable<T> Flatten()
    {
        return new[] { Value }.Concat(_children.SelectMany(x => x.Flatten()));
    }
}