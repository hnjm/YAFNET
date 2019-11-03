﻿using YAF.Lucene.Net.Analysis.TokenAttributes;

namespace YAF.Lucene.Net.Analysis.Cz
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

    /// <summary>
    /// A <see cref="TokenFilter"/> that applies <see cref="CzechStemmer"/> to stem Czech words.
    /// <para>
    /// To prevent terms from being stemmed use an instance of
    /// <see cref="Miscellaneous.SetKeywordMarkerFilter"/> or a custom <see cref="TokenFilter"/> that sets
    /// the <see cref="KeywordAttribute"/> before this <see cref="TokenStream"/>.
    /// </para>
    /// <para><b>NOTE</b>: Input is expected to be in lowercase, 
    /// but with diacritical marks</para> </summary>
    /// <seealso cref="Miscellaneous.SetKeywordMarkerFilter"/>
    public sealed class CzechStemFilter : TokenFilter
    {
        private readonly CzechStemmer stemmer = new CzechStemmer();
        private readonly ICharTermAttribute termAtt;
        private readonly IKeywordAttribute keywordAttr;

        public CzechStemFilter(TokenStream input)
            : base(input)
        {
            termAtt = AddAttribute<ICharTermAttribute>();
            keywordAttr = AddAttribute<IKeywordAttribute>();
        }

        public override bool IncrementToken()
        {
            if (m_input.IncrementToken())
            {
                if (!keywordAttr.IsKeyword)
                {
                    int newlen = stemmer.Stem(termAtt.Buffer, termAtt.Length);
                    termAtt.Length = newlen;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}