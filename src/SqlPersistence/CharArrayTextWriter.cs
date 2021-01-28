﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

sealed class CharArrayTextWriter : TextWriter
{
    internal const int InitialSize = 4096;
    static readonly Encoding EncodingValue = new UnicodeEncoding(false, false);
    char[] _chars = new char[InitialSize];
#pragma warning disable IDE0032 // Use auto property
    int _next;
#pragma warning restore IDE0032 // Use auto property
    int _length = InitialSize;

    public override Encoding Encoding => EncodingValue;

    public override void Write(char value)
    {
        Ensure(1);
        _chars[_next] = value;
        _next += 1;
    }

    void Ensure(int i)
    {
        var required = _next + i;
        if (required < _length)
        {
            return;
        }

        while (required >= _length)
        {
            _length *= 2;
        }
        Array.Resize(ref _chars, _length);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Ensure(count);
        Array.Copy(buffer, index, _chars, _next, count);
        _next += count;
    }

    public override void Write(string value)
    {
        var length = value.Length;
        Ensure(length);
        value.CopyTo(0, _chars, _next, length);
        _next += length;
    }

    public override Task WriteAsync(char value)
    {
        Write(value);
        return CompletedTask;
    }

    public override Task WriteAsync(string value)
    {
        Write(value);
        return CompletedTask;
    }

    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        return CompletedTask;
    }

    public override Task WriteLineAsync(char value)
    {
        WriteLine(value);
        return CompletedTask;
    }

    public override Task WriteLineAsync(string value)
    {
        WriteLine(value);
        return CompletedTask;
    }

    public override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        WriteLine(buffer, index, count);
        return CompletedTask;
    }

    public override Task FlushAsync()
    {
        return CompletedTask;
    }

    public void Release()
    {
        Clear();
        pool.Push(this);
    }

    static readonly ConcurrentStack<CharArrayTextWriter> pool = new ConcurrentStack<CharArrayTextWriter>();
    static readonly Task CompletedTask = Task.FromResult(true);

    public static CharArrayTextWriter Lease()
    {
        if (pool.TryPop(out var writer))
        {
            return writer;
        }

        return new CharArrayTextWriter();
    }

    public ArraySegment<char> ToCharSegment()
    {
        return new ArraySegment<char>(_chars, 0, _next);
    }

    void Clear()
    {
        _next = 0;
    }

#pragma warning disable IDE0032 // Use auto property
    public int Size => _next;
#pragma warning restore IDE0032 // Use auto property
}