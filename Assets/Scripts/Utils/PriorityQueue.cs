using System;
using System.Collections.Generic;

public class PriorityQueue<T>
{
    private List<(T Item, float Priority)> heap = new();
    public int Count => heap.Count;

    public T Peek()
    {
        if (heap.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty!");
        }

        return heap[0].Item;
    }

    public void Enqueue(T item, float priority)
    {
        heap.Add((item, priority));
        HeapifyUp(heap.Count - 1);
    }

    public T Dequeue()
    {
        if (heap.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty!");
        }

        T rootItem = heap[0].Item;

        heap[0] = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);

        if (heap.Count > 0)
        {
            HeapifyDown(0);
        }

        return rootItem;
    }

    private void HeapifyDown(int index)
    {
        while (true)
        {
            int left = index * 2 + 1;
            int right = index * 2 + 2;
            int smallest = index;

            if (left < heap.Count && heap[left].Priority < heap[smallest].Priority)
            {
                smallest = left;
            }

            if (right < heap.Count && heap[right].Priority < heap[smallest].Priority)
            {
                smallest = right;
            }

            if (smallest != index)
            {
                Swap(index, smallest);
                index = smallest;
            }
            else
            {
                break;
            }
        }
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;

            if (heap[index].Priority < heap[parent].Priority)
            {
                Swap(index, parent);
                index = parent;
            }
            else
            {
                break;
            }
        }
    }

    private void Swap(int i, int j)
    {
        (heap[i], heap[j]) = (heap[j], heap[i]);
    }
}
