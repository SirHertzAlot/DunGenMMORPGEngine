def findLongestSubStringThatContainsTwoCharacters(string, c1, c2):
  longest = ""
  for i in range(len(string)):
    for j in range(i+1, len(string)):
      if string[i] == c1 and string[j] == c2:
        if len(string[i:j+1]) > len(longest):
          longest = string[i:j+1]
  return longest
  

print(findLongestSubStringThatContainsTwoCharacters("abcabcbbaababaaaberbeasbabababsabdabfbabsfbasbbfabababsdfsafasfgagrrtggsgdrg", 'a', 'b'))