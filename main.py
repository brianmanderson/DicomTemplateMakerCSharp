import pydicom
from owlready2 import *
import pickle


def save_obj(obj, path): # Save almost anything.. dictionary, list, etc.
    if path[-4:] != '.pkl':
        path += '.pkl'
    with open(path, 'wb') as f:
        pickle.dump(obj, f, pickle.DEFAULT_PROTOCOL)
    return None


def load_obj(path):
    if path[-4:] != '.pkl':
        path += '.pkl'
    if os.path.exists(path):
        with open(path, 'rb') as f:
            return pickle.load(f)
    else:
        out = {}
        return out


def walk_down(onto, i, global_output):
    print(i.preferred_name)
    for child_class in onto.get_children_of(i):
        if len(onto.get_children_of(child_class)) == 0:
            try:
                global_output[child_class.preferred_name[0]] = {'name': child_class.name, 'iri': child_class.iri}
            except:
                print(f"Failed on {child_class}")
        else:
            walk_down(onto, child_class, global_output)
    return None


ds = pydicom.read_file(r'C:\Users\b5anderson\Modular_Projects\DicomTemplateMakerCSharp\DicomTemplateMakerGUI\Test.dcm')
onto = get_ontology(r'C:\Users\b5anderson\Downloads\fma.owl').load()
classes = onto.classes()
organ_class = None

for i in classes:
    if len(i.preferred_name) > 0:
        if i.preferred_name[0] == 'Organ':
            organ_class = i
            break
global_output = dict()
walk_down(onto, organ_class, global_output)
save_obj(global_output, r'./ontology.pkl')
# onto.get_namespace("http://purl.org/sig/ont/fma.owl#")
# xxx = 1