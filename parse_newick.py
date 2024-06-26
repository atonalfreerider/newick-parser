"""
A simple function `parse_newick` to parse tree structures specified in the 
Newick format (NHX supported). The function allows to customize the final
representation, be it lists, dicts or a custom Tree class.

Usage
-----
```
    def tree_as_dict(label, children, distance, features):
        if children:
            return {label: children}
        else:
            return label

    parse_newick('(A,(B,C));', aggregator=tree_as_dict)

    # Output -> {'': ['A', {'': ['B', 'C']}]}
```

The function allows to customize the parsing of the raw feature and distance 
strings with the keword-arguments `feature_parser` and `distance_parser`, for 
example, to read features in the NHX (New Hampshire eXtended Newick) format, you 
can use the following:
```
    def parse_NHX_features(feature_string):
        '''
        Example: 
          '&&NHX:conf=0.01:name=INTERNAL' -> {'conf':'0.01', 'name': 'INTERNAL'}
        '''
        prefix = '&&NHX:'
        feature_string = feature_string[len(prefix):]
        items = feature_string.split(':')
        key_value_pairs = (item.split('=') for item in items)
        return dict(key_value_pairs)

    def tree_builder(label, children, distance, features):
        return {'label': label, 'children': children, 'features': features}
    
    parse_newick(
        'A[&&NHX:conf=0.01:name=A];',
        aggregator=tree_builder,
        feature_parser=parse_NHX_features
    )

    # Output -> 
    # {'label': 'A', 'children': [], 'features': {'conf':'0.01', 'name': 'A'}}
```

To run tests: `pytest parse_newick.py`
To type check: `mypy parse_newick.py`
To run coverage analysis: `coverage run --source=. -m pytest parse_newick.py`
"""

from typing import (Optional, List, Any, Sequence, Tuple, TypeVar, Callable)
from functools import partial

def _find_closing(
        string: str, 
        start: int = 1, 
        pair: Sequence[str] = '()') -> int:
    """
    Find the index of the first closing parenthesis that is not matched to an 
    opening one starting from the given `start` position. 

    Example: '((),())()' -> 6
                    ^

    The `pair` argument allows to specify diferent opening/closing types
    of parenthesis.

    Example:
        _find_closing_brackets = partial(_find_closing, pair='[]')
    """
    opening = pair[0]
    closing = pair[1]

    next_closing = string.index(closing, start)

    has_next_opening = (string.find(opening, start) != -1)
    if not has_next_opening:
        return next_closing
    
    next_opening = string.index(opening, start)
    if next_closing < next_opening:
        return next_closing

    skip = _find_closing(string, next_opening+1, pair)
    return _find_closing(string, skip+1, pair)
# ---

_find_closing_parenthesis = partial(_find_closing, pair='()')
# ---

def _parts_of_subtree(newick: str) -> Tuple[str, str, str, str]:
    """
    A subtree consists of:
        children, label, branch length/support, comments/features

    Example:
        '(A,B)root:10.0[x=xx]' -> ['(A,B)', 'root', '10.0', 'x=xx']
    """

    children = ''
    if newick.startswith('('):
        # Node has children
        children_end = _find_closing_parenthesis(newick)
        children = newick[1:children_end]
        rest = newick[children_end+1:]
    else:
        rest = newick

    comment = ''
    if '[' in rest: # Extract comments from the end
        rest, comment = rest.split('[', maxsplit=1)
        comment = comment[:-1] # remove ']' from the end

    if ':' in rest: # Extract branch length
        label, length = rest.split(':', maxsplit=1)
    else:
        label = rest
        length = ''

    return children, label.strip(), length, comment
# ---

def _next_node_end(nodes_str: str) -> int:
    """
    From a comma-sepparated list of newick-formatted nodes, return the final 
    position of the first one.

    Examples: 
        '(A:1,(C[x],D))name:1.[c], (X,Y),,[xxx]' -> 23
        '(X,Y),,[xxx]' -> 5
        '[xxx]' -> 4
    """
    nodes_str = nodes_str.strip()

    current_end = 0
    # Skip children
    if nodes_str.startswith('('):
        current_end = _find_closing_parenthesis(nodes_str)
    
    # Skip label, distances and comments
    # Strategy: find the next comma that is not surrounded by brackets or
    #    parentheses or the end of the string
    grouping = {'(':'()', '[':'[]'}
    while current_end < len(nodes_str):

        char = nodes_str[current_end]

        if char in grouping:
            current_end = _find_closing(nodes_str, current_end+1, grouping[char])
            continue

        if char == ',':
            # Ah, the lonely comma...
            return current_end-1
        
        current_end += 1
    else:
        return len(nodes_str)-1
# ---

def _split_nodes(nodes_str: str) -> List[str]:
    """
    Separate the nodes from a comma-sepparated list.

    Example:  '(a,b), , :12, c[xxx]' -> ['(a,b)', '', ':12', 'c[xxx]']
    """

    nodes_str = nodes_str.strip()

    if nodes_str == '':
        return []

    if nodes_str == ',':
        return ['', '']

    if nodes_str.startswith(','):
        rest = nodes_str[1:]
        return [''] + _split_nodes(rest)

    if nodes_str.endswith(','):
        rest = nodes_str[:-1]
        return _split_nodes(rest) + ['']

    next_node_end = _next_node_end(nodes_str)
    node = nodes_str[:next_node_end+1]
    rest = nodes_str[next_node_end+1:].lstrip()

    if rest.startswith(','): 
        rest = rest[1:]

    return [node] + _split_nodes(rest)
# ---


def _simple_distance(dist: str) -> Optional[float]: 
    return float(dist) if dist else None

def _simple_feature(feat:str) -> str: 
    return feat

Distance = Any
Feature = Any
Tree = TypeVar('Tree')

def parse_newick_subtree(
        newick: str, 
        aggregator: Callable[[str, List[Tree], Distance, Feature], Tree],
        distance_parser: Callable[[str], Distance] = _simple_distance,
        feature_parser: Callable[[str], Feature] = _simple_feature) -> Tree:
    """
    Parse recursively the newick representation of a single newick node. 
    Root and child nodes are assembled using the aggregator function.

    Example: 
    ```
    def tree_as_list(label, children, distance, features):
        if not children: return label
        return children

    parse_newick_subtree('(A,(B,C))', tree_as_list)

    # Output -> ['A', ['B', 'C']]
    ```

    The distance parser and the feature parser allow to modify the parsing of
    the raw strings, for example, to read features in the NHX (extended Newick) 
    format, you can use the following:
    ```
    def parse_NHX_features(feature_string):
        '''
        Example: 
          '&&NHX:conf=0.01:name=INTERNAL' -> {'conf':'0.01', 'name': 'INTERNAL'}
        '''
        prefix = '&&NHX:'
        feature_string = feature_string[len(prefix):]
        items = feature_string.split(':')
        key_value_pairs = (item.split('=') for item in items)
        return dict(key_value_pairs)

    def tree_builder(label, children, distance, features):
        return {'label': label, 'children': children, 'features': features}
    
    parse_newick_subtree(
        'A[&&NHX:conf=0.01]',
        aggregator=tree_builder,
        feature_parser=parse_NHX_features
    )

    # Output -> 
    # {'label': 'A', 'children': [], 'features': {'conf':'0.01'}}
    """

    parts = _parts_of_subtree(newick.strip())
    (children_str, label, distance_str, comment_str) = parts
    
    children = [parse_newick_subtree(
                    child_str, 
                    aggregator, 
                    distance_parser=distance_parser, 
                    feature_parser=feature_parser) 
                for child_str in _split_nodes(children_str)]
    
    features = feature_parser(comment_str)
    distance = distance_parser(distance_str)

    return aggregator(label, children, distance, features)
# ---

def parse_newick(
        newick: str, 
        aggregator: Callable[[str, List[Tree], Distance, Feature], Tree],
        distance_parser: Callable[[str], Distance] = _simple_distance,
        feature_parser: Callable[[str], Feature] = _simple_feature) -> Tree:
    """
    Parse recursively the newick representation of a complete newick tree. 
    Root and child nodes are assembled using the aggregator function.

    Example: 
    ```
    def tree_as_list(label, children, distance, features):
        if not children: return label
        return children

    parse_newick('(A,(B,C));', tree_as_list)

    # Output -> ['A', ['B', 'C']]
    ```

    The distance parser and the feature parser allow to modify the parsing of
    the raw strings, for example, to read features in the NHX (extended Newick) 
    format, you can use the following:
    ```
    def parse_NHX_features(feature_string):
        '''
        Example: 
          '&&NHX:conf=0.01:name=INTERNAL' -> {'conf':'0.01', 'name': 'INTERNAL'}
        '''
        prefix = '&&NHX:'
        feature_string = feature_string[len(prefix):]
        items = feature_string.split(':')
        key_value_pairs = (item.split('=') for item in items)
        return dict(key_value_pairs)

    def tree_builder(label, children, distance, features):
        return {'label': label, 'children': children, 'features': features}
    
    parse_newick(
        'A[&&NHX:conf=0.01:name=A];',
        aggregator=tree_builder,
        feature_parser=parse_NHX_features
    )

    # Output -> 
    # {'label': 'A', 'children': [], 'features': {'conf':'0.01', 'name': 'A'}}
    ```
    """

    if not newick.endswith(';'):
        raise ValueError("Tree in Newick format must end with ';'")

    root = newick[:-1]
    return parse_newick_subtree(
        root, 
        aggregator, 
        distance_parser=distance_parser, 
        feature_parser=feature_parser
    )