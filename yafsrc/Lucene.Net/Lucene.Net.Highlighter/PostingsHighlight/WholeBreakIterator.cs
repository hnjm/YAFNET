﻿#if FEATURE_BREAKITERATOR
using ICU4N.Text;
using System;
using ICU4N.Support.Text;

namespace YAF.Lucene.Net.Search.PostingsHighlight
{
    /*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements.  See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License.  You may obtain a copy of the License at
	 *
	 *     http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */

    /// <summary>Just produces one single fragment for the entire text</summary>
    public sealed class WholeBreakIterator : BreakIterator
    {
        private CharacterIterator text;
        private int start; 
        private int end; 
        private int current;

        public override int Current
        {
            get { return current; }
        }

        public override int First()
        {
            return (current = start);
        }

        public override int Following(int pos)
        {
            if (pos < start || pos > end)
            {
                throw new ArgumentException("offset out of bounds");
            }
            else if (pos == end)
            {
                // this conflicts with the javadocs, but matches actual behavior (Oracle has a bug in something)
                // http://bugs.sun.com/bugdatabase/view_bug.do?bug_id=9000909
                current = end;
                return Done;
            }
            else
            {
                return Last();
            }
        }

        public override CharacterIterator Text
        {
            get { return text; }
        }

        public override int Last()
        {
            return (current = end);
        }

        public override int Next()
        {
            if (current == end)
            {
                return Done;
            }
            else
            {
                return Last();
            }
        }

        public override int Next(int n)
        {
            if (n < 0)
            {
                for (int i = 0; i < -n; i++)
                {
                    Previous();
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    Next();
                }
            }
            return Current;
        }

        public override int Preceding(int pos)
        {
            if (pos < start || pos > end)
            {
                throw new ArgumentException("offset out of bounds");
            }
            else if (pos == start)
            {
                // this conflicts with the javadocs, but matches actual behavior (Oracle has a bug in something)
                // http://bugs.sun.com/bugdatabase/view_bug.do?bug_id=9000909
                current = start;
                return Done;
            }
            else
            {
                return First();
            }
        }

        public override int Previous()
        {
            if (current == start)
            {
                return Done;
            }
            else
            {
                return First();
            }
        }

        public override bool IsBoundary(int offset)
        {
            if (offset == 0)
            {
                return true;
            }
            int boundary = Following(offset - 1);
            if (boundary == Done)
            {
                throw new ArgumentException();
            }
            return boundary == offset;
        }

        public override void SetText(CharacterIterator newText)
        {
            start = newText.BeginIndex;
            end = newText.EndIndex;
            text = newText;
            current = start;
        }
    }
}
#endif