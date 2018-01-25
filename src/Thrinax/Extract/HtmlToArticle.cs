﻿/*
 * Based on Author: StanZhai 翟士丹 (mail@zhaishidan.cn). All rights reserved.
 * 
 * Modify By Carey Tzou at 13/01/2018.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License. 
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and limitations under the License.
*/

using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Thrinax.Models;
using Thrinax.Utility;
using System.Linq;

namespace Thrinax.Extract
{
    /// <summary>
    /// 解析Html页面的文章正文内容,基于文本密度的HTML正文提取类   
    /// </summary>
    public class HtmlToArticle
    {
        #region 参数设置

        // 正则表达式过滤：正则表达式，要替换成的文本
        private static readonly string[][] Filters =
        {
            new[] { @"(?is)<script.*?>.*?</script>", "" },
            new[] { @"(?is)<style.*?>.*?</style>", "" },
            new[] { @"(?is)<!--.*?-->", "" },    // 过滤Html代码中的注释
            // 针对链接密集型的网站的处理，主要是门户类的网站，降低链接干扰
            new[] { @"(?is)</a>", "</a>\n"},
            new[] { @"<h-char unicode=.*?""><h-inner>([\\s\\S]*?)?</h-inner></h-char>", "$1"}
        };
        #endregion

        /// <summary>
        /// 从给定的Html原始文本中获取正文信息
        /// 只有单条链接情况，正文缺少推倒去重复
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static Article GetArticle(string html)
        {
            // 常见的段内标签去除
            if (html.Contains("<h-char unicode="))
            {
                html = Regex.Replace(html, "", "$1");
            }

            // 获取html，body标签内容
            string backupHtml = html;
            // 过滤样式，脚本等不相干标签
            foreach (var filter in Filters)
            {
                backupHtml = Regex.Replace(backupHtml, filter[0], filter[1]);
            }

            Article article = new Article();

            article.Title = Regex.Replace(GetTitle(html), @"\s", "");
            string dateTimeStr = GetPublishDate(backupHtml);

            article.PubDate = DateTimeParser.Parser(dateTimeStr);
            article.HtmlContent = GetHtmlContent(backupHtml, article.Title, dateTimeStr);

            if (!string.IsNullOrWhiteSpace(article.HtmlContent))
                article.Content = HTMLCleaner.CleanHTML(article.HtmlContent, false);

            return article;
        }

        /// <summary>
        /// 获取标题
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private static string GetTitle(string html)
        {
            string titleFilter = @"<title>[\s\S]*?</title>";
            string h1Filter = @"<h1.*?>.*?</h1>";
            string clearFilter = @"<.*?>";

            string title = "";
            Match match = Regex.Match(html, titleFilter, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                title = Regex.Replace(match.Groups[0].Value, clearFilter, "");
            }

            // 正文的标题一般在h1中，比title中的标题更干净
            match = Regex.Match(html, h1Filter, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string h1 = Regex.Replace(match.Groups[0].Value, clearFilter, "");
                if (!String.IsNullOrEmpty(h1) && title.Contains(h1))
                {
                    title = h1;
                }
            }
            return title;
        }

        /// <summary>
        /// 获取文章发布日期
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private static string GetPublishDate(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            string maybeDateStr = @"0123456789分钟前小时秒半天昨年月日期间发表于布稿出:：/-.更新上线星期周";

            // 过滤html标签，防止标签对日期提取产生影响
            string text = Regex.Replace(html, "(?is)<.*?>", "");
            Match match = Regex.Match(
                text,
                @"((\d{4}|\d{2})(\-|\/)\d{1,2}\3\d{1,2})(\s?\d{2}:\d{2})?|(\d{4}年\d{1,2}月\d{1,2}日)(\s?\d{2}:\d{2})?",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                try
                {
                    string dateStr = "";
                    for (int i = 0; i < match.Groups.Count; i++)
                    {
                        dateStr = match.Groups[i].Value;
                        if (!String.IsNullOrEmpty(dateStr))
                        {
                            break;
                        }
                    }
                    return dateStr;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            else
            {
                //解析所有文本的块，获取包含maybeDateStr的字符串，对长短和命中率进行打分排序
                HtmlNode htmlNode = HtmlUtility.getSafeHtmlRootNode(html, true, true);
                var itemDateNodes = htmlNode.SelectNodes("//*");
                if (itemDateNodes != null && itemDateNodes.Count > 0)
                {
                    Dictionary<string, double> dateStrDic = new Dictionary<string, double>();
                    foreach (var itemDateNode in itemDateNodes)
                    {
                        string _itemDateText = Regex.Replace(itemDateNode.InnerText, @"\s", "");
                        if (!string.IsNullOrWhiteSpace(_itemDateText) && maybeDateStr.Any(f=> _itemDateText.Contains(f)) && _itemDateText.Length < 30)
                        {
                            dateStrDic[_itemDateText] = maybeDateStr.Count(f => _itemDateText.Contains(f)) * 2 - Math.Abs(6 - _itemDateText.Length);
                        }
                    }

                    if (dateStrDic != null && dateStrDic.Count > 0)
                        return dateStrDic.OrderByDescending(f => f.Value).FirstOrDefault().Key;
                }
            }
            return "";
        }

        /// <summary>
        /// Gets the content of the html.
        /// </summary>
        /// <returns>The html content.</returns>
        /// <param name="bodyText">Body text.</param>
        /// <param name="Title">Title.</param>
        /// <param name="dateTimeStr">Date time string.</param>
        private static string GetHtmlContent(string bodyText, string Title, string dateTimeStr)
        {
            string baseHtmlContent = bodyText;

            //首先通过Html的div标签拆解元素打分
            List<Tuple<string, double>> listNodes = SplitHtmlTextByBlockElement(bodyText);
            if (listNodes != null && listNodes.Count > 0)
            {
                baseHtmlContent = listNodes.OrderByDescending(f => f.Item2).FirstOrDefault().Item1;
            }

            return baseHtmlContent;
        }

        /// <summary>
        /// 通过Html常用的块状元素来拆分数据块，第一阶段先用 div
        /// div 默认会有15分的基础分，子元素出现一次div 扣 3 分
        /// 内部包含的正文字每20个得一分，出现一个 a 标签扣除2分
        /// 正文中连续的3个空行会扣除一分，每多一个多扣0.2分
        /// </summary>
        /// <param name="htmlContent"></param>
        /// <returns></returns>
        public static List<Tuple<string, double>> SplitHtmlTextByBlockElement(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
                return null;

            HtmlNode itempagenode = HtmlUtility.getSafeHtmlRootNode(htmlContent, true, true).SelectSingleNode("//body");
            var itemNodes = itempagenode.SelectNodes("//div");

            if (itemNodes == null || itemNodes.Count == 0)
                return null;

            List<Tuple<string, double>> itemNodeScores = new List<Tuple<string, double>>();
            foreach (var itemNode in itemNodes)
            {
                double baseScore = 15;

                HtmlNode innerNode = HtmlUtility.getSafeHtmlRootNode(itemNode.OuterHtml, true, true).SelectSingleNode("//body");

                //子元素出现一次div且内容不为空的 扣 3 分
                var innerDivs = innerNode.SelectNodes("//div");
                if (innerDivs != null && innerDivs.Count > 1)
                {
                    foreach (var innerDiv in innerDivs)
                    {
                        if(!string.IsNullOrWhiteSpace(innerDiv.InnerText))
                            baseScore -= 3;
                    }
                }

                //子元素出现一次a 扣 2 分
                var inneras = innerNode.SelectNodes("//a");
                if (inneras != null && inneras.Count > 0)
                {
                    baseScore -= inneras.Count * 2;
                }

                //获取正文部分，计算字数
                string innerText = itemNode.InnerText;
                if (!string.IsNullOrWhiteSpace(innerText))
                {
                    string innerTextWithoutBlack = Regex.Replace(innerText, @"\s", "");
                    if (!string.IsNullOrWhiteSpace(innerTextWithoutBlack) && innerTextWithoutBlack.Length > 0)
                    {
                        baseScore += (double)innerTextWithoutBlack.Length / 20;

                        //Node中出现超过2个P标签的，同时判断所有 P 元素的字数 与 innerTextWithoutBlack 的字数，差值小于10%时替换，P 元素每增加一个得一分；
                        if (innerNode.SelectSingleNode("//div").ChildNodes.Count(f => f.Name == "p") > 2)
                        {
                            HtmlNode htmlNode = HtmlUtility.getSafeHtmlRootNode("", true, true).SelectSingleNode("//body");
                            foreach (HtmlNode _tmpNode in innerNode.SelectSingleNode("//div").ChildNodes)
                            {
                                if (_tmpNode.Name == "p")
                                {
                                    htmlNode.AppendChild(_tmpNode);
                                }
                            }

                            string newPNodeText = htmlNode.InnerText;
                            if (!string.IsNullOrWhiteSpace(newPNodeText))
                            {
                                string newPNodeTexttWithoutBlack = Regex.Replace(newPNodeText, @"\s", "");

                                if (!string.IsNullOrWhiteSpace(newPNodeTexttWithoutBlack) && newPNodeTexttWithoutBlack.Length > 0
                                    && ((double)(innerTextWithoutBlack.Length - newPNodeTexttWithoutBlack.Length) / innerTextWithoutBlack.Length <= 0.1))
                                {
                                    innerNode = htmlNode;
                                    innerText = newPNodeText;
                                    baseScore = 15 + newPNodeTexttWithoutBlack.Length / 20 + (htmlNode.ChildNodes.Count - 2);
                                }
                            }
                        }
                    }
                }
                else
                    continue;

                //计算正文中的空行，连续的3个空行会扣除一分，每多一个多扣0.2分
                string[] orgLines = innerText.Split('\n');
                int currentBlackCount = 0;
                foreach (string orgLine in orgLines)
                {
                    if (string.IsNullOrWhiteSpace(orgLine))
                    {
                        currentBlackCount++;
                        if (currentBlackCount == 3)
                            baseScore--;
                        else if (currentBlackCount > 3)
                            baseScore -= 0.2;
                    }
                    else
                        currentBlackCount = 0;
                }

                Tuple<string, double> tuple = new Tuple<string, double>(innerNode.InnerHtml, baseScore);
                itemNodeScores.Add(tuple);
            }

            return itemNodeScores.OrderByDescending(f => f.Item2).ToList();
        }
    }
}