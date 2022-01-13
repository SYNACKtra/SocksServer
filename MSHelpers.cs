namespace DiagnosticUtility {
    using System;

    class ExceptionUtility {
        public static Exception ThrowHelperError(Exception ex) {
            return ex;
        }
    }

    class Utility {
        public static byte[] AllocateByteArray(int size) {
            return new byte[size];
        }
    }
}

namespace Fx {

	public class FxTrace {
	    public enum TraceEventType {
	        Information
	    }

	    public class Exception {
	        public static void TraceHandledException(System.Exception ex, TraceEventType lEventType) {
	            System.Console.WriteLine("TraceException");
	            System.Console.WriteLine(ex.ToString());
	        }
	    }
	}


    class Fx {
        public static bool Assert(bool statement, string message) {
            if(!statement) {
                throw new System.Exception(message);
            }
            return statement;    
        }
    }

}


// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==

namespace MS {
	    // This file defines an internal class used to throw exceptions in BCL code.
    // The main purpose is to reduce code size. 
    // 
    // The old way to throw an exception generates quite a lot IL code and assembly code.
    // Following is an example:
    //     C# source
    //          throw new ArgumentNullException("key", Environment.GetResourceString("ArgumentNull_Key"));
    //     IL code:
    //          IL_0003:  ldstr      "key"
    //          IL_0008:  ldstr      "ArgumentNull_Key"
    //          IL_000d:  call       string System.Environment::GetResourceString(string)
    //          IL_0012:  newobj     instance void System.ArgumentNullException::.ctor(string,string)
    //          IL_0017:  throw
    //    which is 21bytes in IL.
    // 
    // So we want to get rid of the ldstr and call to Environment.GetResource in IL.
    // In order to do that, I created two enums: ExceptionResource, ExceptionArgument to represent the
    // argument name and resource name in a small integer. The source code will be changed to 
    //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key, ExceptionResource.ArgumentNull_Key);
    //
    // The IL code will be 7 bytes.
    //    IL_0008:  ldc.i4.4
    //    IL_0009:  ldc.i4.4
    //    IL_000a:  call       void System.ThrowHelper::ThrowArgumentNullException(valuetype System.ExceptionArgument)
    //    IL_000f:  ldarg.0
    //
    // This will also reduce the Jitted code size a lot. 
    //
    // It is very important we do this for generic classes because we can easily generate the same code 
    // multiple times for different instantiation. 
    // 
    // <
 
 
 
 
 
 
 
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Security.Permissions;
    using System.Collections;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Runtime;
    using Fx;


	// If we ever implement more interfaces on IReadOnlyCollection, we should also update RuntimeTypeCache.PopulateInterfaces() in rttype.cs
	
    // A simple Queue of generic objects.  Internally it is implemented as a 
	    // circular buffer, so Enqueue can be O(n).  Dequeue is O(1).
#if !SILVERLIGHT
    [Serializable()]        
#endif
    [System.Runtime.InteropServices.ComVisible(false)]
    public class NetworkQueue : IEnumerable,
        System.Collections.ICollection {
        private SocketAsyncEventArgs[] _array;
        private int _head;       // First valid element in the queue
        private int _tail;       // Last valid element in the queue
        private int _size;       // Number of elements.
        private int _version;
#if !SILVERLIGHT
        [NonSerialized]
#endif
        private Object _syncRoot;
 
        private const int _MinimumGrow = 4;
        private const int _ShrinkThreshold = 32;
        private const int _GrowFactor = 200;  // double each time
        private const int _DefaultCapacity = 4;
        static SocketAsyncEventArgs[]  _emptyArray = new SocketAsyncEventArgs[0];
        
        // Creates a queue with room for capacity objects. The default initial
        // capacity and grow factor are used.
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Queue"]/*' />
        public NetworkQueue()
        {
            _array = _emptyArray;            
        }
    
        // Creates a queue with room for capacity objects. The default grow factor
        // is used.
        //
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Queue1"]/*' />
        public NetworkQueue(int capacity)
        {
            if (capacity < 0)
            	throw new Exception("NetworkQueue capacity lower than 0");

            _array = new SocketAsyncEventArgs[capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }
        
        // Fills a Queue with the elements of an ICollection.  Uses the enumerator
        // to get each of the elements.
        //
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Queue3"]/*' />
        public NetworkQueue(IEnumerable collection)
        {
            if (collection == null)
                throw new Exception("null collection given to NetworkQueue");
 
            _array = new SocketAsyncEventArgs[_DefaultCapacity];
            _size = 0;
            _version = 0;
 			
 			try {
 				QueueEnumerator en = (QueueEnumerator) collection.GetEnumerator();
                while(en.MoveNext()) {
                    Enqueue(en.Current);
                }
	 		} catch(Exception e) {
	 			Console.WriteLine("Exception");
	 		}
        }
    

        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Count"]/*' />
        public int Count
        {
            get { return _size; }
        }
        
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.IsSynchronized"]/*' />
        bool System.Collections.ICollection.IsSynchronized
        {
            get { return false; }
        }
 
        Object System.Collections.ICollection.SyncRoot
        {
            get { 
                if( _syncRoot == null) {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);    
                }
                return _syncRoot;                 
            }
        }
                        
        // Removes all Objects from the queue.
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Clear"]/*' />
        public void Clear()
        {
            if (_head < _tail)
                Array.Clear(_array, _head, _size);
            else {
                Array.Clear(_array, _head, _array.Length - _head);
                Array.Clear(_array, 0, _tail);
            }
    
            _head = 0;
            _tail = 0;
            _size = 0;
            _version++;
        }
    
        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        // 
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.CopyTo"]/*' />
        public void CopyTo(SocketAsyncEventArgs[] array, int arrayIndex)
        {
            if (array==null) {
                throw new Exception("null array to copy to");
            }
 
            if (arrayIndex < 0 || arrayIndex > array.Length) {
            	throw new Exception("Array index to start copying from is too large or non existent");
            }
 
            int arrayLen = array.Length;
            if (arrayLen - arrayIndex < _size) {
            	throw new Exception("Size is greater than the index");
            }
    
            int numToCopy = (arrayLen - arrayIndex < _size) ? (arrayLen - arrayIndex) : _size;
            if (numToCopy == 0) return;
            
            int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
            Array.Copy(_array, _head, array, arrayIndex, firstPart);
            numToCopy -= firstPart;
            if (numToCopy > 0) {
                Array.Copy(_array, 0, array, arrayIndex+_array.Length - _head, numToCopy);
       		}
        }
 
        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            if (array == null) {
            	throw new Exception("Array to copy to is null");
            }
 
            if (array.Rank != 1) {
            	throw new Exception("Array dimension not supported");
            }
 
            if( array.GetLowerBound(0) != 0 ) {
            	throw new Exception("Non zero lower bound in array");
            }
 
            int arrayLen = array.Length;
            if (index < 0 || index > arrayLen) {
            	throw new Exception("Index out of bounds of array");
            }
 
            if (arrayLen - index < _size) {
            	throw new Exception("Index larger than array");
            }
    
            int numToCopy = (arrayLen-index < _size) ? arrayLen-index : _size;
            if (numToCopy == 0) return;
            
            try {
	            int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
	            Array.Copy(_array, _head, array, index, firstPart);
	            numToCopy -= firstPart;
 
                if (numToCopy > 0) {
                	Array.Copy(_array, 0, array, index+_array.Length - _head, numToCopy);
                }
            }
            catch(ArrayTypeMismatchException) {
            	throw new Exception("Array Type mismatch");
            }
        }
        
        // Adds item to the tail of the queue.
        //
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Enqueue"]/*' />
        public void Enqueue(SocketAsyncEventArgs item) {
            if (_size == _array.Length) {
                int newcapacity = (int)((long)_array.Length * (long)_GrowFactor / 100);
                if (newcapacity < _array.Length + _MinimumGrow) {
                    newcapacity = _array.Length + _MinimumGrow;
                }
                SetCapacity(newcapacity);
            }
    
            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            _size++;
            _version++;
        }
    
        // GetEnumerator returns an IEnumerator over this Queue.  This
        // Enumerator will support removing.
        // 
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.GetEnumerator"]/*' />
        public QueueEnumerator GetEnumerator()
        {
            return new QueueEnumerator(this);
        }
 		
 		// System.Collections.IEnumerator System.Collections.Generic.IEnumerable<System.Net.Sockets.SocketAsyncEventArgs>.GetEnumerator()
 		// {
 		// 	return new QueueEnumerator(this);
 		// }
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.IEnumerable.GetEnumerator"]/*' />
        /// <internalonly/>
        // System.Collections.IEnumerator<SocketAsyncEventArgs> System.Collections.IEnumerable.GetEnumerator()
        // {
        //     return new QueueEnumerator(this);
        // }
 
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new QueueEnumerator(this);
        }
    
        // Removes the object at the head of the queue and returns it. If the queue
        // is empty, this method simply returns null.
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Dequeue"]/*' />
        public SocketAsyncEventArgs Dequeue() {
            if (_size == 0)
            	throw new Exception("Empty queue");
    
            SocketAsyncEventArgs removed = _array[_head];
            _array[_head] = default(SocketAsyncEventArgs);
            _head = (_head + 1) % _array.Length;
            _size--;
            _version++;
            return removed;
        }
    
        // Returns the object at the head of the queue. The object remains in the
        // queue. If the queue is empty, this method throws an 
        // InvalidOperationException.
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Peek"]/*' />
        public SocketAsyncEventArgs Peek()
        {
            if (_size == 0)
            	throw new Exception("Empty queue");
    
            return _array[_head];
        }
    
        // Returns true if the queue contains at least one object equal to item.
        // Equality is determined using item.Equals().
        //
        // Exceptions: ArgumentNullException if item == null.
       /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.Contains"]/*' />
       public bool Contains(SocketAsyncEventArgs item)
       {
            int index = _head;
            int count = _size;
 
            //EqualityComparer<SocketAsyncEventArgs> c = EqualityComparer<SocketAsyncEventArgs>.Default;
            while (count-- > 0) {
                if (((Object) item) == null) {
                    if (((Object) _array[index]) == null)
                        return true;
                } 
                else if (_array[index] != null && _array[index].UserToken == item.UserToken) {
                    return true;
                }
                index = (index + 1) % _array.Length;
            }
    
            return false;
        }        
    
        internal SocketAsyncEventArgs GetElement(int i)
        {
            return _array[(_head + i) % _array.Length];
        }
    
        // Iterates over the objects in the queue, returning an array of the
        // objects in the Queue, or an empty array if the queue is empty.
        // The order of elements in the array is first in to last in, the same
        // order produced by successive calls to Dequeue.
        /// <include file='doc\Queue.uex' path='docs/doc[@for="Queue.ToArray"]/*' />
        public SocketAsyncEventArgs[] ToArray()
        {
            SocketAsyncEventArgs[] arr = new SocketAsyncEventArgs[_size];
            if (_size==0)
                return arr;
    
            if (_head < _tail) {
                Array.Copy(_array, _head, arr, 0, _size);
            } else {
                Array.Copy(_array, _head, arr, 0, _array.Length - _head);
                Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
            }
    
            return arr;
        }
    
    
        // PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
        // must be >= _size.
        private void SetCapacity(int capacity) {
            SocketAsyncEventArgs[] newarray = new SocketAsyncEventArgs[capacity];
            if (_size > 0) {
                if (_head < _tail) {
                    Array.Copy(_array, _head, newarray, 0, _size);
                } else {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }
    
            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
            _version++;
        }
    
        public void TrimExcess() {
            int threshold = (int)(((double)_array.Length) * 0.9);             
            if( _size < threshold ) {
                SetCapacity(_size);
            }
        }    
        

	    // Implements an enumerator for a Queue.  The enumerator uses the
	    // internal version number of the list to ensure that no modifications are
	    // made to the list while an enumeration is in progress.
	    /// <include file='doc\Queue.uex' path='docs/doc[@for="QueueEnumerator"]/*' />
	#if !SILVERLIGHT
	    [Serializable()]
	#endif
	    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
	    public struct QueueEnumerator : System.Collections.IEnumerator
	    {
	        private NetworkQueue _q;
	        private int _index;   // -1 = not started, -2 = ended/disposed
	        private int _version;
	        private SocketAsyncEventArgs _currentElement;

	        internal QueueEnumerator(NetworkQueue q) {
	            _q = q;
	            _version = _q._version;
	            _index = -1;
	            _currentElement = default(SocketAsyncEventArgs);
	        }

	        /// <include file='doc\Queue.uex' path='docs/doc[@for="QueueEnumerator.Dispose"]/*' />
	        public void Dispose()
	        {
	            _index = -2;
	            _currentElement = default(SocketAsyncEventArgs);
	        }

	        /// <include file='doc\Queue.uex' path='docs/doc[@for="QueueEnumerator.MoveNext"]/*' />
	        public bool MoveNext() {
	            if (_version != _q._version) throw new Exception("Invalid queue version");
	            
	            if (_index == -2)
	                return false;

	            _index++;

	            if (_index == _q._size) {
	                _index = -2;
	                _currentElement = default(SocketAsyncEventArgs);
	                return false;
	            }
	            
	            _currentElement = _q.GetElement(_index);
	            return true;
	        }

	        /// <include file='doc\Queue.uex' path='docs/doc[@for="QueueEnumerator.Current"]/*' />
	        public SocketAsyncEventArgs Current {
	            get {
	                if (_index < 0)
	                {
	                    if (_index == -1)
	                    	throw new Exception("enumerator not started");
	                    else
	                    	throw new Exception("enumerator ended");
	                }
	                return _currentElement;
	            }
	        }

	        Object System.Collections.IEnumerator.Current {
	            get {
	                if (_index < 0)
	                {
	                    if (_index == -1)
	                    	throw new Exception("enumerator not started");
	                    else
	                    	throw new Exception("enumerator ended");
	                }
	                return _currentElement;
	            }
	        }

	        void System.Collections.IEnumerator.Reset() {
	            if (_version != _q._version) throw new Exception("enumerator version mismatch");
	            _index = -1;
	            _currentElement = default(SocketAsyncEventArgs);
	        }
	    }        
    }


     
    // This is the base object pool class which manages objects in a FIFO queue. The objects are 
    // created through the provided Func<T> createObjectFunc. The main purpose for this class is
    // to get better memory usage for Garbage Collection (GC) when part or all of an object is
    // regularly pinned. Constantly creating such objects can cause large Gen0 Heap fragmentation
    // and thus high memory usage pressure. The pooled objects are first created in Gen0 heaps and
    // would be eventually moved to a more stable segment which would prevent the fragmentation
    // to happen.
    //
    // The objects are created in batches for better localization of the objects. Here are the
    // parameters that control the behavior of creation/removal:
    // 
    // batchAllocCount: number of objects to be created at the same time when new objects are needed
    //
    // createObjectFunc: func delegate that is used to create objects by sub-classes.
    //
    // maxFreeCount: max number of free objects the queue can store. This is to make sure the memory
    //     usage is bounded.
    //
    public class QueuedSocketAsyncEventArgsPool
    {
        MS.NetworkQueue objectQueue;
        bool isClosed;
        int batchAllocCount;
        int maxFreeCount;
        const int SingleBatchSize = 128 * 1024;
        const int MaxBatchCount = 16;
        const int MaxFreeCountFactor = 4;
        int acceptBufferSize;
        BufferManager m_bufferManager;
 
        public QueuedSocketAsyncEventArgsPool(int acceptBufferSize, BufferManager bufferManager)
        {
            if (acceptBufferSize <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("acceptBufferSize"));
            }
 
            this.acceptBufferSize = acceptBufferSize;
            int batchCount = (SingleBatchSize + acceptBufferSize - 1) / acceptBufferSize;
            if (batchCount > MaxBatchCount)
            {
                batchCount = MaxBatchCount;
            }
 
            Initialize(batchCount, batchCount * MaxFreeCountFactor);
            this.m_bufferManager = bufferManager;
        }

        protected void Initialize(int batchAllocCount, int maxFreeCount)
        {
            if (batchAllocCount <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("batchAllocCount"));
            }
 
            Fx.Assert(batchAllocCount <= maxFreeCount, "batchAllocCount cannot be greater than maxFreeCount");
            this.batchAllocCount = batchAllocCount;
            this.maxFreeCount = maxFreeCount;
            this.objectQueue = new MS.NetworkQueue(batchAllocCount);
        }
 
        object ThisLock
        {
            get
            {
                return this.objectQueue;
            }
        }
 
        public virtual bool BaseReturn(SocketAsyncEventArgs value)
        {
            lock (ThisLock)
            {
                if (this.objectQueue.Count < this.maxFreeCount && ! this.isClosed)
                {
                    this.objectQueue.Enqueue(value);
                    return true;
                }
 
                return false;
            }
        }
 
        public SocketAsyncEventArgs Take()
        {
            lock (ThisLock)
            {
                Fx.Assert(!this.isClosed, "Cannot take an item from closed QueuedObjectPool");
 
                if (this.objectQueue.Count == 0)
                {
                    AllocObjects();
                }
 
                return this.objectQueue.Dequeue();
            }
        }
 
        public void Close()
        {
            lock (ThisLock)
            {
                foreach (SocketAsyncEventArgs item in this.objectQueue)
                {
                    if (item != null)
                    {
                        this.CleanupItem(item);
                    }
                }
 
                this.objectQueue.Clear();
                this.isClosed = true;
            }
        }
 
        void AllocObjects()
        {
            Fx.Assert(this.objectQueue.Count == 0, "The object queue must be empty for new allocations");
            for (int i = 0; i < batchAllocCount; i++)
            {
                this.objectQueue.Enqueue(Create());
            }
        }


        public bool Return(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            CleanupAcceptSocket(socketAsyncEventArgs);
            
            if (!BaseReturn(socketAsyncEventArgs))
            {
                this.CleanupItem(socketAsyncEventArgs);
                return false;
            }
 
            return true;
        }
 
        internal static void CleanupAcceptSocket(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            Fx.Assert(socketAsyncEventArgs != null, "socketAsyncEventArgs should not be null.");
 
            Socket socket = socketAsyncEventArgs.AcceptSocket;
            if (socket != null)
            {
                socketAsyncEventArgs.AcceptSocket = null;
 
                try
                {
                    socket.Close(0);
                }
                catch (SocketException ex)
                {
                    FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                catch (ObjectDisposedException ex)
                {
                    FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
            }
        }
 
        public void CleanupItem(SocketAsyncEventArgs item)
        {    
            item.Dispose();
        }
 
        public SocketAsyncEventArgs Create()
        {
            SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
            eventArgs.SetBuffer(this.m_bufferManager.TakeBuffer(acceptBufferSize), 0, acceptBufferSize);
            return eventArgs;    
        }
    }
}
