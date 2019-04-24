from lxml import etree
import pyproj
import copy
import sys
import getopt
import os

#Checks if point is within bounds (Height not checked, coordinates need to be in EPSG:3857)
def inBounds(point, x1, y1, x2, y2):
	if ((point[0] > x1 and point[0] < x2) and (point[1] < y1 and point[1] > y2)):
		return True
	else:
		return False
		
#Reproject all gml:pos tags, then write to file
def writeBuilding(building, template, outputPath, outName, projection):
	srcProj = pyproj.Proj(projection)
	EPSG4326 = pyproj.Proj("+init=EPSG:4326")
	#Find the highest, northernmost, easternmost point
	#Start with height, keep only elements with highest hight
	center = [0,0,sys.float_info.min]
	posNr = 0
	for position in building.findall('.//gml:pos', namespaces=template.nsmap):
		coords = position.text.split()
		#Remove first two numbers from x (lon), then convert into number
		x = float(coords[0][2:])
		y = float(coords[1])
		z = float(coords[2])
		#Collect positions for x-y center
		center = [center[0] + x, center[1] + y, max(center[2], z)]
		posNr = posNr + 1
	referencePoint = [center[0]/posNr, center[1]/posNr, center[2]]	
	#Check if in bounds
	if(not inBounds(referencePoint, 310661.476651, 5995856.558156, 311805.657554, 5994955.660174)):
		print(outName + " out of bounds!")
		return
	#Reproject reference
	print("Unprojected: " + str(referencePoint[0]) + " " + str(referencePoint[1]) + " " + str(referencePoint[2]))
	lat, lon, height = pyproj.transform(srcProj, EPSG4326, referencePoint[0], referencePoint[1], referencePoint[2])
	centerText = str(lat) + ' ' + str(lon) + ' ' + str(height)
	print("Projected: " + centerText)
	
	#Add center as a tag of the building
	gmlRef = etree.Element(etree.QName(template.nsmap['gml'], 'AbstractFeature'))
	gmlPoint = etree.Element(etree.QName(template.nsmap['gml'], 'point')) 
	gmlPos = etree.Element(etree.QName(template.nsmap['gml'], 'pos'))
	building.append(gmlRef)
	gmlRef.append(gmlPoint)
	gmlPoint.append(gmlPos)
	gmlPos.text = centerText
	gmlPos.attrib['srsDimension'] = '3'
	gmlRef.attrib[etree.QName(template.nsmap['gml'], 'id')] = 'unityReferencePoint'
	
	#Fill template and write to file
	template[0].append(building)
	output = etree.ElementTree(template)
	output.write(os.path.join(outputPath, outName + '.xml'), pretty_print=True, xml_declaration=True, encoding="utf-8")
	print(outName + " written!")

#Reads XML, prepares template xml for the buildings, then starts iteration	
def main(argv):
	#Read arguments
	inputFile = ''
	outputPath = ''
	try:
		opts, args = getopt.getopt(argv,"hi:o:p:",["ifile=","ofile=","proj="])
	except getopt.GetoptError:
		print('test.py -i <inputfile> -o <outputpath>')
		sys.exit(2)
	for opt, arg in opts:
		if opt == '-h':
			print('test.py -i <inputfile> -o <outputpath>')
			sys.exit()
		elif opt in ("-i", "--ifile"):
			inputFile = arg
		elif opt in ("-o", "--ofile"):
			outputPath = arg
		elif opt in ("-p", "--proj"):
			projection = arg
	if (not os.path.exists(inputFile)):
		print('Invalid input file')
	if (not os.path.exists(outputPath)):
		print('Invalid output path')
		
	#Parse XML
	tree = etree.parse(inputFile)
	root = tree.getroot()
	
	#Create template tree
	template = copy.deepcopy(root)
	
	#Create CityGML skeleton-template, down to cityObjectMember level
	firstCityObjectMember = True
	for cityObjectMember in template:
		#Check tag name (without namespace). Keep only first element in cityModel, remove buildings from it
		if etree.QName(cityObjectMember).localname == 'cityObjectMember' and firstCityObjectMember:
			for building in cityObjectMember:
				cityObjectMember.remove(building)
			firstCityObjectMember = False
		else: 
			template.remove(cityObjectMember)
	
	#Process and write the buildings
	count = 0
	for cityObjectMember in root:
		if etree.QName(cityObjectMember).localname == 'cityObjectMember':
			#Iterate buildings
			for building in cityObjectMember:
				count = count + 1
				buildingName = "Building" + str(count)
				#Separate building parts
				#partcount = 0
				#for bldgpart in building:
				#	if etree.QName(bldgpart).localname == 'consistsOfBuildingPart':
				#		partcount = partcount + 1
				#		part = bldgpart[0] #Actual part is one level deeper
				#		part.tag = etree.QName(template.nsmap['bldg'], 'Building') #Rename so converter handles it correctly
				#		partName = buildingName + '#' + str(partcount)
				#		writeBuilding(part, copy.deepcopy(template), outputPath, partName, projection)
				#		building.remove(bldgpart)	
				#Write main building
				writeBuilding(building, copy.deepcopy(template), outputPath, buildingName, projection)
				
			
if __name__ == "__main__":
   main(sys.argv[1:])